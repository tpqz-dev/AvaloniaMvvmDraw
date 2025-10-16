using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaMvvmDraw.Views.Interfaces;

namespace AvaloniaMvvmDraw.Views.Models
{
    public sealed class ImageLayer : IDrawableLayer, IBorderedLayer, IMovableRectLayer
    {
        public Size Size { get; set; }
        public IPen? BorderPen { get; set; }

        public Bitmap? Bitmap { get; set; }

        public ImageLayer(Bitmap bitmap) => Bitmap = bitmap;

        // DestRect de l'image
        public Rect Rect { get; set; }
        public string Name { get; set; } = "image_";

        public void Draw(DrawingContext context)
        {
            if (Bitmap is not null)
            {
                var src = new Rect(new Size(Bitmap.Size.Width, Bitmap.Size.Height));
                context.DrawImage(Bitmap, src, Rect);
            }

            if (BorderPen is not null)
                context.DrawRectangle(null, BorderPen, Rect);
        }

        // Dessine une miniature dans le coin haut-gauche du calque
        public void DrawThumbnailOverlay(DrawingContext context)
        {
            if (Bitmap is null) return;
            var padding = 6;
            var maxThumb = 96.0; // pixels
            var rw = Rect.Width;
            var rh = Rect.Height;
            if (rw <= padding * 2 || rh <= padding * 2) return;
            var thumbW = System.Math.Min(maxThumb, rw - padding * 2);
            var thumbH = System.Math.Min(maxThumb, rh - padding * 2);
            var sx = thumbW / Bitmap.Size.Width;
            var sy = thumbH / Bitmap.Size.Height;
            var s = System.Math.Min(sx, sy);
            thumbW = Bitmap.Size.Width * s;
            thumbH = Bitmap.Size.Height * s;

            var dest = new Rect(Rect.X + padding, Rect.Y + padding, thumbW, thumbH);
            var bg = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
            context.FillRectangle(bg, dest.Inflate(new Thickness(4)));
            context.DrawRectangle(null, new Pen(Brushes.White, 1), dest.Inflate(new Thickness(2)));

            var src = new Rect(new Size(Bitmap.Size.Width, Bitmap.Size.Height));
            context.DrawImage(Bitmap, src, dest);
        }
    }
}
