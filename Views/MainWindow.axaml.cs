using Avalonia.Controls;
using AvaloniaMvvmDraw.Views;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void AboutMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var aboutWindow = new AboutSmallWindow();
            await aboutWindow.ShowDialog(this);
        }
    }
}