using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private void AddLayerButton_Click(object? sender, RoutedEventArgs e)
        {
            // Ajoute un nouveau calque rectangle et le sélectionne
            var newLayer = new RectangleLayer(
                new Rect(100, 100, 120, 80),
                Brushes.LightGreen,
                new Pen(Brushes.Black, 1));

            drawingSurface.Layers.Add(newLayer);
            drawingSurface.SelectedDrawableLayer = newLayer;
        }

        private void RemoveLayerButton_Click(object? sender, RoutedEventArgs e)
        {
            var sel = drawingSurface.SelectedDrawableLayer;
            if (sel is not null)
                drawingSurface.Layers.Remove(sel);
        }
    }
}