using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaMvvmDraw.Views.Interfaces;
using AvaloniaMvvmDraw.Views.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;

namespace AvaloniaMvvmDraw.Views
{
    public partial class DrawingSurface : Control
    {
        private Matrix _transform = Matrix.Identity;
        private Point _lastPanPoint;
        private bool _isPanning;
        private double zoomFactor; // last step factor (for logging only)
        private EventHandler<PointerWheelEventArgs>? _topLevelWheelHandler;
        public ObservableCollection<IDrawableLayer> Layers { get; } = new();

        private static readonly IPen RedLayerBorder = new Pen(Brushes.Red, 1);
        private static readonly IPen YellowLayerBorder = new Pen(Brushes.Yellow, 1);

        private const double MinZoom = 0.05;   // 5%
        private const double MaxZoom = 20.0;   // 2000%
        private const double ZoomStepUp = 1.1; // wheel / keyboard increment factors
        private const double ZoomStepDown = 0.9;
        private const double Epsilon = 1e-6;

        private int _nextLayerNumber = 1; // insertion order counter

        private bool _isMovingLastLayer;
        private IMovableRectLayer? _movingLayer;
        private Point _layerDragStartPointer;
        private Rect _layerDragStartRect;

        private Point _lastPointerPos;
        private bool _hasPointerPos;

        // Transform mode (per-layer)
        private bool _isTransformMode;
        public bool IsTransformMode => _isTransformMode;
        private enum Handle
        {
            None,
            Move,
            // Edge resize
            ResizeLeft, ResizeRight, ResizeTop, ResizeBottom,
            // Corner resize
            ResizeTL, ResizeTR, ResizeBR, ResizeBL,
            // Corner rotate (offset outward)
            RotateTL, RotateTR, RotateBR, RotateBL
        }
        private Handle _activeHandle = Handle.None;
        private Rect _transformStartRect;
        private double _transformStartAngle;
        private Point _transformStartPointerScreen;

        // Per-layer rotation angles (in radians)
        private readonly Dictionary<IDrawableLayer, double> _layerAngles = new();

        public static readonly StyledProperty<double> RotationProperty =
            AvaloniaProperty.Register<DrawingSurface, double>(nameof(Rotation), 0d);

        public double Rotation
        {
            get => GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
        }

        public IBrush? Background
        {
            get => (IBrush?)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }
        public IDrawableLayer? SelectedDrawableLayer
        {
            get => GetValue(SelectedDrawableLayerProperty);
            set => SetValue(SelectedDrawableLayerProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<DrawingSurface, IBrush?>(nameof(Background));

        public static readonly StyledProperty<IDrawableLayer?> SelectedDrawableLayerProperty =
            AvaloniaProperty.Register<DrawingSurface, IDrawableLayer?>(nameof(SelectedDrawableLayer));

        static DrawingSurface()
        {
            RotationProperty.Changed.AddClassHandler<DrawingSurface>((x, _) =>
            {
                x.InvalidateVisual();
                Log.Information("Rotation changed: {Rotation}°", x.Rotation);
            });

            SelectedDrawableLayerProperty.Changed.AddClassHandler<DrawingSurface>((x, _) =>
            {
                x.UpdateLayerBorders();
                x.InvalidateVisual();
            });
        }

        public DrawingSurface()
        {
            Background = Brushes.Transparent;
            Focusable = true;
            Layers.CollectionChanged += Layers_CollectionChanged;
            Layers.Insert(0, new RectangleLayer(new Rect(500, 200, 100, 100), Brushes.CornflowerBlue, new Pen(Brushes.Black, 1)));

            AddHandler(InputElement.PointerWheelChangedEvent,
                (s, e) => OnPointerWheelChanged(e),
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        public void ToggleTransformMode() => SetTransformMode(!_isTransformMode);
        public void SetTransformMode(bool enabled)
        {
            if (_isTransformMode == enabled) return;
            _isTransformMode = enabled;
            _activeHandle = Handle.None;
            Cursor = enabled ? new Cursor(StandardCursorType.Cross) : null;
            InvalidateVisual();
            Log.Information("Transform mode set to {State}", enabled);
        }

        private void UpdateLayerBorders()
        {
            foreach (var l in Layers)
            {
                if (l is IBorderedLayer bl)
                {
                    bl.BorderPen = ReferenceEquals(l, SelectedDrawableLayer)
                        ? YellowLayerBorder
                        : RedLayerBorder;
                }
            }
        }

        public void BeginMoveLastLayer()
        {
            _isMovingLastLayer = false;
            _movingLayer = null;
            _isMovingLastLayer = true;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            Focus();
            InvalidateVisual();
        }

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 })
            {
                IDrawableLayer? last = null;
                foreach (var item in e.NewItems)
                {
                    if (item is IBorderedLayer bordered)
                        bordered.BorderPen = RedLayerBorder;
                    if (item is IDrawableLayer dl)
                    {
                        dl.Name = string.IsNullOrWhiteSpace(dl.Name)
                            ? $"Layer {_nextLayerNumber}"
                            : $"{dl.Name}{_nextLayerNumber}";
                        _nextLayerNumber++;
                        last = dl;
                    }
                }
                if (last is not null) SelectedDrawableLayer = last;
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SelectedDrawableLayer = Layers.Count > 0 ? Layers[^1] : null;
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: > 0 })
            {
                var removedSelected = false;
                foreach (var item in e.OldItems)
                    removedSelected |= ReferenceEquals(item, SelectedDrawableLayer);
                if (removedSelected)
                {
                    SelectedDrawableLayer = Layers.Count > 0
                        ? Layers[Math.Min(Math.Max(e.OldStartingIndex, 0), Layers.Count - 1)]
                        : null;
                }
                return;
            }
            if (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems is { Count: > 0 })
            {
                SelectedDrawableLayer = e.NewItems[^1] as IDrawableLayer;
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var tl = TopLevel.GetTopLevel(this);
            _topLevelWheelHandler ??= (_, args) =>
            {
                var pos = args.GetPosition(this);
                if (pos.X >= 0 && pos.Y >= 0 && pos.X <= Bounds.Width && pos.Y <= Bounds.Height)
                    OnPointerWheelChanged(args);
            };
            tl?.AddHandler(InputElement.PointerWheelChangedEvent, _topLevelWheelHandler,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            var tl = TopLevel.GetTopLevel(this);
            if (_topLevelWheelHandler != null)
                tl?.RemoveHandler(InputElement.PointerWheelChangedEvent, _topLevelWheelHandler);
            base.OnDetachedFromVisualTree(e);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            using (context.PushClip(new Rect(Bounds.Size)))
            {
                var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
                var angleRad = Rotation * Math.PI / 180.0;
                var rotation = Matrix.CreateTranslation(new Vector(-center.X, -center.Y)) *
                               Matrix.CreateRotation(angleRad) *
                               Matrix.CreateTranslation(new Vector(center.X, center.Y));
                var tl = TopLevel.GetTopLevel(this);
                var surfaceSize = tl?.ClientSize ?? Bounds.Size;
                using (context.PushTransform(rotation * _transform))
                {
                    foreach (var layer in Layers)
                    {
                        layer.Size = surfaceSize;
                        // Apply per-layer rotation
                        if (layer is IMovableRectLayer m && _layerAngles.TryGetValue(layer, out var a) && Math.Abs(a) > Epsilon)
                        {
                            var rc = m.Rect;
                            var c = new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2);
                            var mtx = Matrix.CreateTranslation(new Vector(-c.X, -c.Y)) *
                                      Matrix.CreateRotation(a) *
                                      Matrix.CreateTranslation(new Vector(c.X, c.Y));
                            using (context.PushTransform(mtx))
                            {
                                layer.Draw(context);
                            }
                        }
                        else
                        {
                            layer.Draw(context);
                        }
                    }
                }
            }
            DrawOverlay(context);
        }

        private void DrawOverlay(DrawingContext context)
        {
            // Overlay with info
            if (_hasPointerPos)
            {
                var world = ScreenToWorld(_lastPointerPos);
                var scale = GetCurrentScale();
                var text = $"World: {world.X:0.##}, {world.Y:0.##}  Zoom: {scale * 100:0.#}%  Rot: {Rotation:0.#}°";
                var formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.White);
                var padding = new Thickness(6, 4, 6, 4);
                var size = new Size(formatted.Width + padding.Left + padding.Right,
                                     formatted.Height + padding.Top + padding.Bottom);
                var origin = new Point(8, Bounds.Height - size.Height - 8);
                var rect = new Rect(origin, size);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), rect, 4);
                context.DrawText(formatted, origin + new Point(padding.Left, padding.Top));
                context.DrawLine(new Pen(Brushes.Yellow, 1), _lastPointerPos + new Vector(-5, 0), _lastPointerPos + new Vector(5, 0));
                context.DrawLine(new Pen(Brushes.Yellow, 1), _lastPointerPos + new Vector(0, -5), _lastPointerPos + new Vector(0, 5));
            }

            // Draw transform handles if enabled
            if (_isTransformMode && SelectedDrawableLayer is IMovableRectLayer sel)
            {
                DrawTransformGizmo(context, sel);
            }
        }

        private void DrawTransformGizmo(DrawingContext context, IMovableRectLayer sel)
        {
            var rc = sel.Rect;
            var angle = GetLayerAngle(SelectedDrawableLayer!);
            var corners = GetRotatedCorners(rc, angle);
            var cornersScreen = new Point[4];
            for (int i = 0; i < 4; i++)
                cornersScreen[i] = WorldToScreen(corners[i]);

            // Outline
            var pen = new Pen(Brushes.Lime, 1);
            for (int i = 0; i < 4; i++)
                context.DrawLine(pen, cornersScreen[i], cornersScreen[(i + 1) % 4]);

            // Handles sizes
            const double hs = 5; // square half-size
            const double rotOffset = 16; // pixels outward from corner
            var centerScreen = WorldToScreen(new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2));

            // Helper to draw square handle
            void DrawSquare(Point p, IBrush fill) => context.FillRectangle(fill, new Rect(p.X - hs, p.Y - hs, hs * 2, hs * 2));
            // Helper to draw rotation circle
            void DrawCircle(Point p, IBrush fill)
            {
                context.DrawEllipse(fill, null, p, hs, hs);
            }

            // Corner resize handles (blue squares at corners)
            DrawSquare(cornersScreen[0], Brushes.DodgerBlue); // TL
            DrawSquare(cornersScreen[1], Brushes.DodgerBlue); // TR
            DrawSquare(cornersScreen[2], Brushes.DodgerBlue); // BR
            DrawSquare(cornersScreen[3], Brushes.DodgerBlue); // BL

            // Edge resize handles (blue squares at edge midpoints)
            DrawSquare(Mid(cornersScreen[0], cornersScreen[1]), Brushes.DodgerBlue); // top
            DrawSquare(Mid(cornersScreen[1], cornersScreen[2]), Brushes.DodgerBlue); // right
            DrawSquare(Mid(cornersScreen[2], cornersScreen[3]), Brushes.DodgerBlue); // bottom
            DrawSquare(Mid(cornersScreen[3], cornersScreen[0]), Brushes.DodgerBlue); // left

            // Rotation handles: at outward-offset positions from corners
            Point RotPos(Point corner)
            {
                var v = corner - centerScreen;
                var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (len < 1) len = 1;
                var nx = v.X / len; var ny = v.Y / len;
                return new Point(corner.X + nx * rotOffset, corner.Y + ny * rotOffset);
            }
            DrawCircle(RotPos(cornersScreen[0]), Brushes.OrangeRed); // TL rot
            DrawCircle(RotPos(cornersScreen[1]), Brushes.OrangeRed); // TR rot
            DrawCircle(RotPos(cornersScreen[2]), Brushes.OrangeRed); // BR rot
            DrawCircle(RotPos(cornersScreen[3]), Brushes.OrangeRed); // BL rot
        }

        private static Point Mid(Point a, Point b) => new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        private Point[] GetRotatedCorners(Rect rc, double angleRad)
        {
            var c = new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2);
            var pts = new[]
            {
                new Point(rc.X, rc.Y),
                new Point(rc.X + rc.Width, rc.Y),
                new Point(rc.X + rc.Width, rc.Y + rc.Height),
                new Point(rc.X, rc.Y + rc.Height)
            };
            for (int i = 0; i < pts.Length; i++)
                pts[i] = RotatePoint(pts[i], c, angleRad);
            return pts;
        }

        private static Point RotatePoint(Point p, Point center, double angleRad)
        {
            var s = Math.Sin(angleRad);
            var c = Math.Cos(angleRad);
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            var x = dx * c - dy * s + center.X;
            var y = dx * s + dy * c + center.Y;
            return new Point(x, y);
        }

        private double GetLayerAngle(IDrawableLayer layer)
        {
            return _layerAngles.TryGetValue(layer, out var a) ? a : 0.0;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if ((e.Key == Key.D0 || e.Key == Key.NumPad0) && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var includeRotation = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                ResetView(includeRotation);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ToggleTransformMode();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Add || (e.Key == Key.OemPlus && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
            {
                ZoomAtCenter(ZoomStepUp);
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract || (e.Key == Key.OemMinus && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
            {
                ZoomAtCenter(ZoomStepDown);
                e.Handled = true;
            }
        }

        private void ZoomAtCenter(double factor)
        {
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            ApplyZoom(center, factor);
        }

        public void ResetView(bool includeRotation)
        {
            _transform = Matrix.Identity;
            if (includeRotation)
                Rotation = 0;
            InvalidateVisual();
            Log.Information("View reset (rotation reset: {IncludeRotation})", includeRotation);
        }

        private void ApplyZoom(Point pivotScreen, double requestedFactor)
        {
            var currentScale = GetCurrentScale();
            var targetScale = currentScale * requestedFactor;
            targetScale = Math.Clamp(targetScale, MinZoom, MaxZoom);
            if (Math.Abs(targetScale - currentScale) < Epsilon)
                return; // no effective change
            var actualFactor = targetScale / currentScale;
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var angle = Rotation * Math.PI / 180.0;
            var dx = pivotScreen.X - center.X;
            var dy = pivotScreen.Y - center.Y;
            var cos = Math.Cos(-angle);
            var sin = Math.Sin(-angle);
            var localX = dx * cos - dy * sin;
            var localY = dx * sin + dy * cos;
            var localPos = new Point(localX + center.X, localY + center.Y);
            _transform = Matrix.CreateTranslation(new Vector(-localPos.X, -localPos.Y)) *
                         Matrix.CreateScale(new Vector(actualFactor, actualFactor)) *
                         Matrix.CreateTranslation(new Vector(localPos.X, localPos.Y)) * _transform;
            InvalidateVisual();
            zoomFactor = actualFactor;
            Log.Information("Zoom applied -> factor {Factor:0.###}, scale now {Scale:0.###}", actualFactor, targetScale);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetCurrentPoint(this);

            if (_isTransformMode && SelectedDrawableLayer is IMovableRectLayer sel && point.Properties.IsLeftButtonPressed)
            {
                var handle = HitTestHandle(sel, point.Position);
                if (handle != Handle.None)
                {
                    _activeHandle = handle;
                    _transformStartRect = sel.Rect;
                    _transformStartAngle = GetLayerAngle(SelectedDrawableLayer!);
                    _transformStartPointerScreen = point.Position;
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }

            if (!_isMovingLastLayer && point.Properties.IsLeftButtonPressed)
            {
                var world = ScreenToWorld(point.Position);
                for (int i = Layers.Count - 1; i >= 0; i--)
                {
                    if (Layers[i] is IMovableRectLayer rectLayer && rectLayer.Rect.Contains(world))
                    {
                        SelectedDrawableLayer = Layers[i];
                        e.Handled = true;
                        InvalidateVisual();
                        break;
                    }
                }
            }

            if (_isMovingLastLayer && point.Properties.IsLeftButtonPressed)
            {
                _movingLayer = SelectedDrawableLayer as IMovableRectLayer;
                if (_movingLayer is null) return;
                _layerDragStartPointer = e.GetPosition(this);
                _layerDragStartRect = _movingLayer.Rect;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            if (point.Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        private Handle HitTestHandle(IMovableRectLayer sel, Point screen)
        {
            const double hs = 7; // hit radius
            var angle = GetLayerAngle(SelectedDrawableLayer!);
            var rc = sel.Rect;
            var corners = GetRotatedCorners(rc, angle);
            var cScreen = new Point[4];
            for (int i = 0; i < 4; i++) cScreen[i] = WorldToScreen(corners[i]);
            var centerScreen = WorldToScreen(new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2));
            Point RotPos(Point corner)
            {
                var v = corner - centerScreen;
                var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (len < 1) len = 1;
                var nx = v.X / len; var ny = v.Y / len;
                return new Point(corner.X + nx * 16, corner.Y + ny * 16);
            }
            // rotation handles first
            if (Near(RotPos(cScreen[0]), screen, hs)) return Handle.RotateTL;
            if (Near(RotPos(cScreen[1]), screen, hs)) return Handle.RotateTR;
            if (Near(RotPos(cScreen[2]), screen, hs)) return Handle.RotateBR;
            if (Near(RotPos(cScreen[3]), screen, hs)) return Handle.RotateBL;
            // corner resize
            if (Near(cScreen[0], screen, hs)) return Handle.ResizeTL;
            if (Near(cScreen[1], screen, hs)) return Handle.ResizeTR;
            if (Near(cScreen[2], screen, hs)) return Handle.ResizeBR;
            if (Near(cScreen[3], screen, hs)) return Handle.ResizeBL;
            // edges resize
            if (Near(Mid(cScreen[0], cScreen[1]), screen, hs)) return Handle.ResizeTop;
            if (Near(Mid(cScreen[1], cScreen[2]), screen, hs)) return Handle.ResizeRight;
            if (Near(Mid(cScreen[2], cScreen[3]), screen, hs)) return Handle.ResizeBottom;
            if (Near(Mid(cScreen[3], cScreen[0]), screen, hs)) return Handle.ResizeLeft;
            // inside for move
            if (PointInRotatedRect(screen, rc, angle)) return Handle.Move;
            return Handle.None;
        }

        private bool PointInRotatedRect(Point screen, Rect rc, double angleRad)
        {
            // Convert screen -> world, then unrotate around rect center, then rect.Contains
            var world = ScreenToWorld(screen);
            var c = new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2);
            var unrot = RotatePoint(world, c, -angleRad);
            return rc.Contains(unrot);
        }

        private static bool Near(Point a, Point b, double radius)
        {
            var dx = a.X - b.X; var dy = a.Y - b.Y;
            return (dx * dx + dy * dy) <= radius * radius;
        }

        private double GetCurrentScale()
        {
            if (Matrix.TryDecomposeTransform(_transform, out var dec))
                return Math.Abs(dec.Scale.X);
            return 1.0;
        }

        private Point ScreenToWorld(Point screen)
        {
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var angleRad = Rotation * Math.PI / 180.0;
            var rotation = Matrix.CreateTranslation(new Vector(-center.X, -center.Y)) *
                           Matrix.CreateRotation(angleRad) *
                           Matrix.CreateTranslation(new Vector(center.X, center.Y));
            var composite = rotation * _transform;
            if (composite.TryInvert(out var inv))
                return inv.Transform(screen);
            return screen;
        }

        private Point WorldToScreen(Point world)
        {
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var angleRad = Rotation * Math.PI / 180.0;
            var rotation = Matrix.CreateTranslation(new Vector(-center.X, -center.Y)) *
                           Matrix.CreateRotation(angleRad) *
                           Matrix.CreateTranslation(new Vector(center.X, center.Y));
            var composite = rotation * _transform;
            return composite.Transform(world);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            _lastPointerPos = e.GetPosition(this);
            _hasPointerPos = true;

            if (_isTransformMode && SelectedDrawableLayer is IMovableRectLayer sel && e.Pointer.Captured == this && _activeHandle != Handle.None)
            {
                var currentScreen = e.GetPosition(this);
                var deltaScreen = currentScreen - _transformStartPointerScreen;
                var scale = GetCurrentScale(); if (scale <= 0) scale = 1;
                var ang = Rotation * Math.PI / 180.0;
                var cos = Math.Cos(ang); var sin = Math.Sin(ang);
                var dx = deltaScreen.X / scale; var dy = deltaScreen.Y / scale;
                var worldDx = dx * cos + dy * sin;
                var worldDy = -dx * sin + dy * cos;

                if (_activeHandle == Handle.Move)
                {
                    sel.Rect = new Rect(_transformStartRect.X + worldDx, _transformStartRect.Y + worldDy, _transformStartRect.Width, _transformStartRect.Height);
                }
                else if (_activeHandle == Handle.ResizeLeft || _activeHandle == Handle.ResizeRight || _activeHandle == Handle.ResizeTop || _activeHandle == Handle.ResizeBottom
                      || _activeHandle == Handle.ResizeTL || _activeHandle == Handle.ResizeTR || _activeHandle == Handle.ResizeBR || _activeHandle == Handle.ResizeBL)
                {
                    var rc0 = _transformStartRect;
                    var center = new Point(rc0.X + rc0.Width / 2, rc0.Y + rc0.Height / 2);
                    var pointerWorld = ScreenToWorld(currentScreen);
                    var local = RotatePoint(pointerWorld, center, -_transformStartAngle);
                    // local is in unrotated world; compute offset from center
                    var lx = local.X - center.X;
                    var ly = local.Y - center.Y;

                    double minSize = 1; // px
                    double minHalf = minSize / 2.0;
                    double hw0 = Math.Max(minHalf, rc0.Width / 2.0);
                    double hh0 = Math.Max(minHalf, rc0.Height / 2.0);
                    double hw = hw0, hh = hh0;
                    bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

                    switch (_activeHandle)
                    {
                        case Handle.ResizeLeft:
                        case Handle.ResizeRight:
                        {
                            double targetHW = Math.Max(minHalf, Math.Abs(lx));
                            if (shift)
                            {
                                double s = targetHW / hw0;
                                hw = Math.Max(minHalf, hw0 * s);
                                hh = Math.Max(minHalf, hh0 * s);
                            }
                            else
                            {
                                hw = targetHW;
                                hh = hh0;
                            }
                            break;
                        }
                        case Handle.ResizeTop:
                        case Handle.ResizeBottom:
                        {
                            double targetHH = Math.Max(minHalf, Math.Abs(ly));
                            if (shift)
                            {
                                double s = targetHH / hh0;
                                hw = Math.Max(minHalf, hw0 * s);
                                hh = Math.Max(minHalf, hh0 * s);
                            }
                            else
                            {
                                hw = hw0;
                                hh = targetHH;
                            }
                            break;
                        }
                        case Handle.ResizeTL:
                        case Handle.ResizeTR:
                        case Handle.ResizeBR:
                        case Handle.ResizeBL:
                        {
                            double targetHW = Math.Max(minHalf, Math.Abs(lx));
                            double targetHH = Math.Max(minHalf, Math.Abs(ly));
                            if (shift)
                            {
                                // proportional scaling around center; choose min when shrinking, max when enlarging
                                double sx = targetHW / hw0;
                                double sy = targetHH / hh0;
                                bool shrinking = sx <= 1 && sy <= 1;
                                double s = shrinking ? Math.Min(sx, sy) : Math.Max(sx, sy);
                                hw = Math.Max(minHalf, hw0 * s);
                                hh = Math.Max(minHalf, hh0 * s);
                            }
                            else
                            {
                                hw = targetHW;
                                hh = targetHH;
                            }
                            break;
                        }
                    }

                    // Rebuild rect centered on center with new half sizes
                    sel.Rect = new Rect(center.X - hw, center.Y - hh, hw * 2.0, hh * 2.0);
                }
                else // rotate
                {
                    var rc0 = _transformStartRect;
                    var center = new Point(rc0.X + rc0.Width / 2, rc0.Y + rc0.Height / 2);
                    var startVec = _transformStartPointerScreen - WorldToScreen(center);
                    var currVec = currentScreen - WorldToScreen(center);
                    var startAng = Math.Atan2(startVec.Y, startVec.X);
                    var currAng = Math.Atan2(currVec.Y, currVec.X);
                    var deltaAng = currAng - startAng;
                    _layerAngles[SelectedDrawableLayer!] = _transformStartAngle + deltaAng;
                }

                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_isMovingLastLayer && _movingLayer is not null && e.Pointer.Captured == this)
            {
                var pos = e.GetPosition(this);
                var deltaScreen = pos - _layerDragStartPointer;
                var scale = GetCurrentScale();
                if (scale <= 0) scale = 1;
                var angle = Rotation * Math.PI / 180.0;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);
                var dx2 = deltaScreen.X / scale;
                var dy2 = deltaScreen.Y / scale;
                var worldDx2 = dx2 * cos + dy2 * sin;
                var worldDy2 = -dx2 * sin + dy2 * cos;
                _movingLayer.Rect = new Rect(
                    _layerDragStartRect.X + worldDx2,
                    _layerDragStartRect.Y + worldDy2,
                    _layerDragStartRect.Width,
                    _layerDragStartRect.Height);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_isPanning && e.Pointer.Captured == this)
            {
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                if (delta != default)
                {
                    _transform = Matrix.CreateTranslation(new Vector(delta.X, delta.Y)) * _transform;
                    _lastPanPoint = currentPoint;
                    InvalidateVisual();
                }
                e.Handled = true;
                return;
            }

            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_isTransformMode && _activeHandle != Handle.None && e.Pointer.Captured == this)
            {
                _activeHandle = Handle.None;
                e.Pointer.Capture(null);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (_isMovingLastLayer && e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (_isPanning && e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
                _isPanning = false;
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _isPanning = false;
            if (_isMovingLastLayer) _movingLayer = null;
            _activeHandle = Handle.None;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (e.Delta.Y == 0) return;
            e.Handled = true;
            var factor = e.Delta.Y > 0 ? ZoomStepUp : ZoomStepDown;
            ApplyZoom(e.GetPosition(this), factor);
        }
    }
}