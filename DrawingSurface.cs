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

            // Positionne l'image à (100,100) avec sa taille d'origine
            var layer = new ImageLayer(bitmap)
            {
                Rect = new Rect(100, 100, bitmap.Size.Width, bitmap.Size.Height)
            };

            // Ajouter au-dessus: dessiné en dernier => au-dessus des anciens calques
            Layers.Add(layer);
            InvalidateVisual();
        }

        // Permet de quitter le mode déplacement
        public void CancelMoveMode()
        {
            _isMovingLastLayer = false;
            _movingLayer = null;
            InvalidateVisual();
        }
    }


}