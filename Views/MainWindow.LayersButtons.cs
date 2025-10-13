using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaMvvmDraw.Views.Models;

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
            var layers = drawingSurface.Layers;
            var sel = drawingSurface.SelectedDrawableLayer;

            if (sel is null || layers.Count==1)
                return;

            var idx = layers.IndexOf(sel);
            if (idx >= 0)
            {
               

                if (layers.Count == 0)
                {
                    // Plus de calques
                    drawingSurface.SelectedDrawableLayer = null;
                }
                else
                {
                    // Sélectionne le calque précédent si possible, sinon le premier
                    var prevIndex = System.Math.Max(0, idx - 1);
                    drawingSurface.SelectedDrawableLayer = layers[prevIndex];
                }
                layers.RemoveAt(idx);
            }
        }

        // Bouton: sélectionner le calque précédent
        private void PreviousLayerButton_Click(object? sender, RoutedEventArgs e)
        {
            var layers = drawingSurface.Layers;
            var current = drawingSurface.SelectedDrawableLayer;
            if (current is null)
                return;

            var index = layers.IndexOf(current);
            if (index > 0)
                drawingSurface.SelectedDrawableLayer = layers[index - 1];
            // Si déjà au premier, on ne change rien
        }
    }
}