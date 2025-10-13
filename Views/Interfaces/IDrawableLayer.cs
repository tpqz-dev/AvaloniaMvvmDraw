using Avalonia;
using Avalonia.Media;

namespace AvaloniaMvvmDraw.Views.Interfaces
{
    public interface IDrawableLayer
    {
        // Chaque calque connaît la taille de la surface (fenêtre)
        Size Size { get; set; }
        string Name { get; set; }
        void Draw(DrawingContext context);
    }
}