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
using AvaloniaMvvmDraw.Views.Interfaces;
using AvaloniaMvvmDraw.Views.Models;

namespace AvaloniaMvvmDraw.Views
{
    public partial class DrawingSurface : Control
    {
        private Matrix _transform = Matrix.Identity;
        private Point _lastPanPoint;
        private bool _isPanning;
        private double zoomFactor;
        private EventHandler<PointerWheelEventArgs>? _topLevelWheelHandler;
        public ObservableCollection<IDrawableLayer> Layers { get; } = new ();

        private static readonly IPen RedLayerBorder = new Pen(Brushes.Red, 1);

        // Mode déplacement du dernier calque
        private bool _isMovingLastLayer;
        private IMovableRectLayer? _movingLayer;
        private Point _layerDragStartPointer;
        private Rect _layerDragStartRect;

        // Suivi position souris
        private Point _lastPointerPos;
        private bool _hasPointerPos;

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

        // Appelé par le bouton "moveButton"
        public void BeginMoveLastLayer()
        {
            _isMovingLastLayer = false;
            _movingLayer = null;

            // Ne bouger que le dernier layer qui a un Rect
            // Désormais: le layer sélectionné via le panel 'layer' sera déplacé
            _isMovingLastLayer = true;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            Focus(); // s'assure de recevoir les événements
            Focus(); // s'assure de recevoir les événements
            InvalidateVisual(); // rafraîchit l'affichage du statut
        }

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Applique la bordure aux nouveaux calques et force la sélection
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 })
            {
                IDrawableLayer? last = null;
                foreach (var item in e.NewItems)
                {
                    if (item is IBorderedLayer bordered)
                        bordered.BorderPen = RedLayerBorder;

                    if (item is IDrawableLayer dl)
                        last = dl;
                }

                if (last is not null)
                    SelectedDrawableLayer = last;

                InvalidateVisual();
                return;
            }

            // Lors d'un chargement/réinitialisation de la collection, sélectionner le dernier calque
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SelectedDrawableLayer = Layers.Count > 0 ? Layers[Layers.Count - 1] : null;
                InvalidateVisual();
                return;
            }

            // Si le calque sélectionné est supprimé, sélectionner le voisin pertinent
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: > 0 })
            {
                var removedSelected = false;
                foreach (var item in e.OldItems)
                    removedSelected |= ReferenceEquals(item, SelectedDrawableLayer);

                if (removedSelected)
                {
                    if (Layers.Count > 0)
                    {
                        var idx = Math.Min(Math.Max(e.OldStartingIndex, 0), Layers.Count - 1);
                        SelectedDrawableLayer = Layers[idx];
                    }
                    else
                    {
                        SelectedDrawableLayer = null;
                    }
                }

                InvalidateVisual();
                return;
            }

            // Remplacement: sélectionner le nouvel élément ciblé
            if (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems is { Count: > 0 })
            {
                SelectedDrawableLayer = e.NewItems[e.NewItems.Count - 1] as IDrawableLayer;
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

            // Ligne 2: position souris + statut du mode déplacement
            var mouseStr = _hasPointerPos ? $"{_lastPointerPos.X:0}, {_lastPointerPos.Y:0}" : "—";
            var moveStr = _isMovingLastLayer ? "Activé" : "Désactivé";
            var y2 = 8 + formattedText.Height + 4;

            var formattedText2 = new FormattedText(
                $"Souris: {mouseStr} | Déplacer calque: {moveStr}",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                16,
                Brushes.Black
            );
            context.DrawText(formattedText2, new Point(8, y2));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            // Déplacement du calque sélectionné avec le clic gauche (après clic sur moveButton)
            if (_isMovingLastLayer && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _movingLayer = SelectedDrawableLayer as IMovableRectLayer;
                if (_movingLayer is null)
                    return;

                _layerDragStartPointer = e.GetPosition(this);
                _layerDragStartRect = _movingLayer.Rect;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            // Pan avec le bouton du milieu
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            // Mise à jour de la position de la souris (affichage overlay)
            _lastPointerPos = e.GetPosition(this);
            _hasPointerPos = true;

            // Continuer à déplacer si nous avons la capture (plus robuste que tester le bouton)
            if (_isMovingLastLayer && _movingLayer is not null && e.Pointer.Captured == this)
            {
                var pos = e.GetPosition(this);
                var deltaScreen = pos - _layerDragStartPointer;

                // Convertit le delta écran -> espace "calques" (monde) en annulant zoom et rotation
                var scale = GetCurrentScale();
                if (scale <= 0) scale = 1;

                var angle = Rotation * Math.PI / 180.0;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);

                var dx = deltaScreen.X / scale;
                var dy = deltaScreen.Y / scale;

                // Appliquer la rotation inverse: R(-angle) * (dx, dy)
                var worldDx = dx * cos + dy * sin;
                var worldDy = -dx * sin + dy * cos;

                _movingLayer.Rect = new Rect(
                    _layerDragStartRect.X + worldDx,
                    _layerDragStartRect.Y + worldDy,
                    _layerDragStartRect.Width,
                    _layerDragStartRect.Height);

                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_isPanning && e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                var currentPoint = e.GetPosition(this);
                var delta = currentPoint - _lastPanPoint;
                _transform = Matrix.CreateTranslation(new Vector(delta.X, delta.Y)) * _transform;
                _lastPanPoint = currentPoint;
                InvalidateVisual();
                return;
            }

            // Rafraîchit l'overlay même sans pan/move
            InvalidateVisual();
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _hasPointerPos = true;
            _lastPointerPos = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _hasPointerPos = false;
            InvalidateVisual();
        }

        private double GetCurrentScale()
        {
            if (Matrix.TryDecomposeTransform(_transform, out var dec))
                return Math.Abs(dec.Scale.X);
            return 1.0;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isMovingLastLayer)
            {
                // Fin du drag uniquement, rester en mode déplacer
                if (e.Pointer.Captured == this)
                    e.Pointer.Capture(null);
                // Ne pas désactiver le mode ni changer le curseur
                e.Handled = true;
                InvalidateVisual();
                return;
            }

           //    _isPanning = false;
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

}