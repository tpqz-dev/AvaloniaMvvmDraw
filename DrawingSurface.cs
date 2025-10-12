using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AvaloniaMvvmDraw.Views
{
    public partial class DrawingSurface
    {
        public async Task LoadImageAsync()
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Ouvrir une image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png","*.jpg","*.jpeg","*.bmp","*.gif","*.tif","*.tiff","*.webp" }
                    }
                }
            });

            if (files is null || files.Count == 0) return;

            await using var stream = await files[0].OpenReadAsync();
            var bitmap = new Bitmap(stream);

            // Ajouter au-dessus: dessiné en dernier => au-dessus des anciens calques
            Layers.Add(new ImageLayer(bitmap));
            InvalidateVisual();
        }
    }

    internal sealed class ImageLayer : IDrawableLayer, IBorderedLayer
    {
        public Size Size { get; set; }
        public IPen? BorderPen { get; set; }

        private readonly Bitmap _bitmap;

        public ImageLayer(Bitmap bitmap) => _bitmap = bitmap;

        public void Draw(DrawingContext context)
        {
            // Dessine l'image à (0,0)
            var src = new Rect(_bitmap.Size);
            var dst = new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height);
            context.DrawImage(_bitmap, src, dst);

            if (BorderPen is not null)
                context.DrawRectangle(null, BorderPen, new Rect(0, 0, Size.Width, Size.Height));
        }
    }
}