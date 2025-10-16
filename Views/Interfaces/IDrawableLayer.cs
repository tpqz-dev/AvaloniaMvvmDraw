using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaloniaMvvmDraw.Views.Interfaces
{
    public interface IDrawableLayer
    {
        // Chaque calque connaît la taille de la surface (fenêtre)
        Size Size { get; set; }
        string Name { get; set; }
        // Image associée (optionnelle) pour les calques pouvant afficher un bitmap
        Bitmap? Bitmap { get; set; }
        void Draw(DrawingContext context);
    }
}