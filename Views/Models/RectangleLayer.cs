using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaMvvmDraw.Views.Interfaces;
using System;

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

        // Optionally carry a bitmap (forced to asset icon for rectangle layers)
        public Bitmap? Bitmap { get; set; }
        private const string DefaultIconUri = "avares://AvaloniaMvvmDraw/Assets/video-2d-icon.png";

        public RectangleLayer(Rect rect, IBrush? fill, IPen? pen)
        {
            Rect = rect;
            _fill = fill;
            _pen = pen;
            TryLoadDefaultIcon();
        }

        private void TryLoadDefaultIcon()
        {
            try
            {
                using var s = AssetLoader.Open(new Uri(DefaultIconUri));
                Bitmap = new Bitmap(s);
            }
            catch
            {
                // ignore if asset missing
            }
        }

        public void Draw(DrawingContext context)
        {
            // Utilise la bordure rouge imposée (ou le stylo fourni en fallback)
            context.DrawRectangle(_fill, BorderPen ?? _pen, Rect);
        }
    }
}

