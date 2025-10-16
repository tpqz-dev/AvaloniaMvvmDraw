using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Media;
using System;
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

            // Selection change guard during drag
            layersList.SelectionChanged += LayersList_SelectionChanged;

            // Drag/drop (kept no-op now but enabled if needed)
            layersList.AddHandler(DragDrop.DragOverEvent, LayersList_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            layersList.AddHandler(DragDrop.DropEvent, LayersList_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            DragDrop.SetAllowDrop(layersList, true);
        }

        private bool _overviewDragging;
        private Point _overviewStart;
        private Vector _overviewOffset;

        private void OverviewHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var header = (Border)sender!;
            var panel = (Border)header.Parent!.Parent!;
            var tt = (TranslateTransform)panel.RenderTransform!;
            _overviewDragging = true;
            _overviewStart = e.GetPosition(this);
            _overviewOffset = new Vector(tt.X, tt.Y);
            e.Pointer.Capture(header);
            e.Handled = true;
        }

        private void OverviewHeader_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_overviewDragging) return;
            var header = (Border)sender!;
            var panel = (Border)header.Parent!.Parent!;
            var tt = (TranslateTransform)panel.RenderTransform!;
            var pos = e.GetPosition(this);
            var delta = pos - _overviewStart;
            tt.X = _overviewOffset.X + delta.X;
            tt.Y = _overviewOffset.Y + delta.Y;
            e.Handled = true;
        }

        private void OverviewHeader_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_overviewDragging)
            {
                _overviewDragging = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void OverviewHeader_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _overviewDragging = false;
        }

        private void ResetViewButton_Click(object? sender, RoutedEventArgs e)
        {
            // Same as Ctrl+Shift+0: reset pan+zoom+rotation
            drawingSurface.Focus();
            drawingSurface.ResetView(true);
            Log.Information("Reset view by button click");
        }

        private async void AboutMenu_Click(object? sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutSmallWindow();
            await aboutWindow.ShowDialog(this);
            Log.Information("About menu");
        }

        private void ExitMenu_Click(object? sender, RoutedEventArgs e)
        {
            Log.Information("Exit menu");
            Close();
        }

        private void ToggleTransformButton_Click(object? sender, RoutedEventArgs e)
        {
            // Turn off move mode when toggling transform
            drawingSurface.CancelMoveMode();
            drawingSurface.ToggleTransformMode();
            drawingSurface.Focus();
            Log.Information("Transform mode toggled from Tools panel");
        }
    }
}