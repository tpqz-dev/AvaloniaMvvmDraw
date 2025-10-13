using Avalonia;
using Avalonia.Media;
using AvaloniaMvvmDraw.Views.Interfaces;

namespace AvaloniaMvvmDraw.Views.Models
{
    public sealed class RectangleLayer : IDrawableLayer, IBorderedLayer, IMovableRectLayer
    {
        public Size Size { get; set; } // mis à jour par DrawingSurface

        public Rect Rect { get; set; }
        private readonly IBrush? _fill;
        private readonly IPen? _pen;

        public IPen? BorderPen { get; set; }
        public string Name { get; set; } = "layer_";

        public RectangleLayer(Rect rect, IBrush? fill, IPen? pen)
        {
            Rect = rect;
            _fill = fill;
            _pen = pen;
        }

        public void Draw(DrawingContext context)
        {
            // Utilise la bordure rouge imposée (ou le stylo fourni en fallback)
            context.DrawRectangle(_fill, BorderPen ?? _pen, Rect);
        }
    }
}

