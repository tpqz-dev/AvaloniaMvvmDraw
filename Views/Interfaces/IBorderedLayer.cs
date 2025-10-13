using Avalonia.Media;

namespace AvaloniaMvvmDraw.Views.Interfaces
{
    // Calque qui peut recevoir une bordure imposée par la surface
    public interface IBorderedLayer
    {
        IPen? BorderPen { get; set; }
    }
}