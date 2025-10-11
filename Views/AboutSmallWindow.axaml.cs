using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Serilog;


namespace AvaloniaMvvmDraw;

public partial class AboutSmallWindow : Window
{
    public AboutSmallWindow() 
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.Information("close");
        Close();
    }

    private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Permet de déplacer la fenêtre en cliquant n'importe où
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}