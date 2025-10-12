using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Serilog;
using System.Collections.Generic;
using System.Globalization;

namespace AvaloniaMvvmDraw.Views
{
    public class DrawingSurface : Control
    {
        private Matrix _transform = Matrix.Identity;
        private Point _lastPanPoint;
        private bool _isPanning;
        private double zoomFactor;

        public DrawingSurface()
        {
            // Capte la molette même si un parent l’a déjà gérée (ScrollViewer, etc.)   
            AddHandler(InputElement.PointerWheelChangedEvent,
                (s, e) => OnPointerWheelChanged(e),
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        public List<IDrawableLayer> Layers { get; } = new ();

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // Dessiner le fond en LightGray
            context.DrawRectangle(Brushes.LightGray, pen: null, new Rect(Bounds.Size));

            using (context.PushTransform(_transform))
            {
                // Dessiner chaque calque
                foreach (var layer in Layers)
                {
                    layer.Draw(context);
                }
            }

            // Dessiner le zoom en haut à gauche (coordonnées écran)
            if (Matrix.TryDecomposeTransform(_transform, out var dec))
                zoomFactor = System.Math.Abs(dec.Scale.X);

            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
            var formattedText = new FormattedText(
                $"Zoom: {zoomFactor:0.00}x",
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

            e.Handled = true; // Empêche un ScrollViewer parent d'intercepter la molette

            zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            var position = e.GetPosition(this);
            _transform = Matrix.CreateTranslation(new Vector(-position.X, -position.Y)) *
                         Matrix.CreateScale(new Vector(zoomFactor, zoomFactor)) *
                         Matrix.CreateTranslation(new Vector(position.X, position.Y)) * _transform;

            InvalidateVisual();

            Log.Information($"Zoom step: {zoomFactor:0.00}x");
        }
    }

    public interface IDrawableLayer
    {
        void Draw(DrawingContext context);
    }
}