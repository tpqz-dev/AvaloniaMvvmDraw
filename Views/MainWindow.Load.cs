using Avalonia.Controls;
using Avalonia.Interactivity;

using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        // Pseudocode:
        // - Récupérer le contrôle nommé "drawingSurface" de type DrawingSurface.
        // - Si non trouvé, quitter.
        // - Appeler LoadImageAsync() en await pour charger l'image.

        private async void LoadMenu_Click(object? sender, RoutedEventArgs e)
        {
            var surface = this.FindControl<DrawingSurface>("drawingSurface");
            if (surface is null) return;

            await surface.LoadImageAsync();
        }
    }
}