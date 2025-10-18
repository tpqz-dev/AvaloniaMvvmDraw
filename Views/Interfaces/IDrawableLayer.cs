using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaloniaMvvmDraw.Views.Interfaces
{
    public interface IDrawableLayer
    {
        // Each layer knows the size of the drawing surface (window)
        Size Size { get; set; }
        string Name { get; set; }
        // Associated image (optional) for layers that can display a bitmap
        Bitmap? Bitmap { get; set; }
        void Draw(DrawingContext context);
    }
}