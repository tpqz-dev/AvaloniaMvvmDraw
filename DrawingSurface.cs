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

            Layers.Insert(0, new ImageLayer(bitmap));
            InvalidateVisual();
        }
    }

    internal sealed class ImageLayer : IDrawableLayer
    {
        public Size Size { get; set; }

        private readonly Bitmap _bitmap;

        public ImageLayer(Bitmap bitmap) => _bitmap = bitmap;

        public void Draw(DrawingContext context)
        {
            var sz = _bitmap.Size; // taille en DIP
            var src = new Rect(sz);
            var dst = new Rect(0, 0, sz.Width, sz.Height);
            context.DrawImage(_bitmap, src, dst);
        }
    }
}   