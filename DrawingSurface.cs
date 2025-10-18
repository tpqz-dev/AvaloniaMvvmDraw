using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaMvvmDraw.Views.Interfaces;
using AvaloniaMvvmDraw.Views.Models;

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
                Title = "Open an image",
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

            // Position the image at (100,100) with its original size
            var layer = new ImageLayer(bitmap)
            {
                Rect = new Rect(100, 100, bitmap.Size.Width, bitmap.Size.Height)
            };

            // Add on top: drawn last => above older layers
            Layers.Add(layer);
            InvalidateVisual();
        }

        // Exit move mode
        public void CancelMoveMode()
        {
            _isMovingLastLayer = false;
            _movingLayer = null;
            InvalidateVisual();
        }
    }


}