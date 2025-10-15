using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using System;
using AvaloniaMvvmDraw.Views;
using Serilog;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Pointer handlers: tunnel + bubble + handledEventsToo to always receive them
            layersList.AddHandler(InputElement.PointerPressedEvent, LayersList_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            layersList.AddHandler(InputElement.PointerMovedEvent, LayersList_PointerMoved,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            layersList.AddHandler(InputElement.PointerReleasedEvent, LayersList_PointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

            // Drag/drop (kept no-op now but enabled if needed)
            layersList.AddHandler(DragDrop.DragOverEvent, LayersList_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            layersList.AddHandler(DragDrop.DropEvent, LayersList_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            DragDrop.SetAllowDrop(layersList, true);
        }

        private async void AboutMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var aboutWindow = new AboutSmallWindow();
            await aboutWindow.ShowDialog(this);
            Log.Information("About menu");
        }

        private void ExitMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Log.Information("Exit menu");
            Close();
        }
    }
}