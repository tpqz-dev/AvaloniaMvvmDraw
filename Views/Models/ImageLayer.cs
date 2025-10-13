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

        private readonly Bitmap _bitmap;

        public ImageLayer(Bitmap bitmap) => _bitmap = bitmap;

        // DestRect de l'image
        public Rect Rect { get; set; }
        public string Name { get; set; } = "image_";

        public void Draw(DrawingContext context)
        {
            // Source = taille intrinsèque du bitmap, Destination = Rect positionnable
            var src = new Rect(new Size(_bitmap.Size.Width, _bitmap.Size.Height));
            context.DrawImage(_bitmap, src, Rect);

            if (BorderPen is not null)
                context.DrawRectangle(null, BorderPen, Rect);
        }
    }
}
