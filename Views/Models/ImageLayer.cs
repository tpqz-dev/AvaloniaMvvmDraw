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

        // Original bitmap and metadata
        public Bitmap? Bitmap { get; set; }

        // Basic image info requested: path, filename, original dimensions
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public double ImageWidth { get; set; }
        public double ImageHeight { get; set; }
        public double OriginalAspectRatio { get; set; }

        // Per-layer rotation (degrees)
        public double Rotation { get; set; }

        public ImageLayer(Bitmap bitmap, string? fileName = null, string? filePath = null)
        {
            Bitmap = bitmap;
            ImageWidth = bitmap.Size.Width;
            ImageHeight = bitmap.Size.Height;
            // Store original aspect ratio (w/h) for later use
            OriginalAspectRatio = ImageHeight != 0 ? ImageWidth / ImageHeight : 0.0;
            FileName = fileName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Rotation = 0.0;
        }

        // Image destination rect
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

        // Draw a thumbnail in the top-left corner of the layer
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
