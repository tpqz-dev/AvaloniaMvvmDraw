using Avalonia;

namespace AvaloniaMvvmDraw.Views
{
    // Calque déplaçable via un Rect
    public interface IMovableRectLayer
    {
        Rect Rect { get; set; }
    }
}