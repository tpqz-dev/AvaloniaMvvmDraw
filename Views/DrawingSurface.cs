using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;

namespace AvaloniaMvvmDraw.Views
{
    public partial class DrawingSurface : Control
    {
        private Matrix _transform = Matrix.Identity;
        private Point _lastPanPoint;
        private bool _isPanning;
        private double zoomFactor;
        private EventHandler<PointerWheelEventArgs>? _topLevelWheelHandler;
        private static readonly IPen RedLayerBorder = new Pen(Brushes.Red, 1);

        public static readonly StyledProperty<double> RotationProperty =
            AvaloniaProperty.Register<DrawingSurface, double>(nameof(Rotation), 0d);

        public double Rotation
        {
            get => GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
        }

        // Ajoutez cette propriété pour exposer Background
        public IBrush? Background
        {
            get => (IBrush?)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<DrawingSurface, IBrush?>(nameof(Background));

        static DrawingSurface()
        {
            RotationProperty.Changed.AddClassHandler<DrawingSurface>((x, e) =>
            {
                x.InvalidateVisual();
                Log.Information("Rotation changed: {Rotation}°", x.Rotation);
            });
        }

        public DrawingSurface()
        {
            // S'assure que le contrôle est ciblable au hit-test
            Background = Brushes.Transparent;

            // Imposer une bordure rouge 1px à tout calque ajouté
            Layers.CollectionChanged += Layers_CollectionChanged;

            // Ajoute un rectangle 100x100 sur le calque 0 à la position (500, 200)
            Layers.Insert(0, new RectangleLayer(new Rect(500, 200, 100, 100), Brushes.CornflowerBlue, new Pen(Brushes.Black, 1)));

            // Capte la molette même si un parent l’a déjà gérée (ScrollViewer, etc.)
            AddHandler(InputElement.PointerWheelChangedEvent,
                (s, e) => OnPointerWheelChanged(e),
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is { Count: > 0 })
            {
                foreach (var item in e.NewItems)
                {
                    if (item is IBorderedLayer bordered)
                        bordered.BorderPen = RedLayerBorder;
                }
                InvalidateVisual();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var tl = TopLevel.GetTopLevel(this);
            _topLevelWheelHandler ??= (s, args) =>
            {
                // Utilise un test géométrique plutôt que IsPointerOver (ignore le hit-test)
                var pos = args.GetPosition(this);
                if (pos.X >= 0 && pos.Y >= 0 && pos.X <= Bounds.Width && pos.Y <= Bounds.Height)
                {   
                    OnPointerWheelChanged(args);
                }
            };
            tl?.AddHandler(
                InputElement.PointerWheelChangedEvent,
                _topLevelWheelHandler,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true
            );
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            var tl = TopLevel.GetTopLevel(this);
            if (_topLevelWheelHandler != null)
                tl?.RemoveHandler(InputElement.PointerWheelChangedEvent, _topLevelWheelHandler);
            base.OnDetachedFromVisualTree(e);
        }

        public ObservableCollection<IDrawableLayer> Layers { get; } = new ();

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var angleRad = Rotation * Math.PI / 180.0;
            var rotation = Matrix.CreateTranslation(new Vector(center.X, center.Y)) *
                           Matrix.CreateRotation(angleRad) *
                           Matrix.CreateTranslation(new Vector(-center.X, -center.Y));

            // Taille de référence = taille de la fenêtre principale (fallback: taille du contrôle)
            var tl = TopLevel.GetTopLevel(this);
            var surfaceSize = tl?.ClientSize ?? Bounds.Size;

            using (context.PushTransform(rotation * _transform))
            {
                foreach (var layer in Layers)
                {
                    layer.Size = surfaceSize; // chaque calque prend la taille de la fenêtre
                    layer.Draw(context);
                }
            }

            if (Matrix.TryDecomposeTransform(_transform, out var dec))
                zoomFactor = Math.Abs(dec.Scale.X);

            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
            var formattedText = new FormattedText(
                $"Zoom: {zoomFactor:0.00}x | Rotation: {Rotation:0}°",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                16,
                Brushes.Black
            );
            context.DrawText(formattedText, new Point(8, 8));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _isPanning = false;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_isPanning && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                _transform = Matrix.CreateTranslation(new Vector(delta.X, delta.Y)) * _transform;
                _lastPanPoint = currentPoint;
                InvalidateVisual();
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (e.Delta.Y == 0) return;

            e.Handled = true;

            // Calcul du pivot de zoom dans l'espace "avant rotation"
            var position = e.GetPosition(this);
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var angle = Rotation * Math.PI / 180.0;
            var dx = position.X - center.X;
            var dy = position.Y - center.Y;
            var cos = Math.Cos(-angle);
            var sin = Math.Sin(-angle);
            var localX = dx * cos - dy * sin;
            var localY = dx * sin + dy * cos;
            var localPos = new Point(localX + center.X, localY + center.Y);

            zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;

            _transform = Matrix.CreateTranslation(new Vector(-localPos.X, -localPos.Y)) *
                         Matrix.CreateScale(new Vector(zoomFactor, zoomFactor)) *
                         Matrix.CreateTranslation(new Vector(localPos.X, localPos.Y)) * _transform;

            InvalidateVisual();

            Log.Information("Wheel handled -> Zoom step: {ZoomStep:0.00}x, Rotation: {Rotation}°", zoomFactor, Rotation);
        }
    }

    public interface IDrawableLayer
    {
        // Chaque calque connaît la taille de la surface (fenêtre)
        Size Size { get; set; }
        void Draw(DrawingContext context);
    }

    // Calque qui peut recevoir une bordure imposée par la surface
    public interface IBorderedLayer
    {
        IPen? BorderPen { get; set; }
    }

    // Calque simple pour dessiner un rectangle
    public sealed class RectangleLayer : IDrawableLayer, IBorderedLayer
    {
        public Size Size { get; set; } // mis à jour par DrawingSurface

        private readonly Rect _rect;
        private readonly IBrush? _fill;
        private readonly IPen? _pen;

        public IPen? BorderPen { get; set; }

        public RectangleLayer(Rect rect, IBrush? fill, IPen? pen)
        {
            _rect = rect;
            _fill = fill;
            _pen = pen;
        }

        public void Draw(DrawingContext context)
        {
            // Utilise la bordure rouge imposée (ou le stylo fourni en fallback)
            context.DrawRectangle(_fill, BorderPen ?? _pen, _rect);
        }
    }
}