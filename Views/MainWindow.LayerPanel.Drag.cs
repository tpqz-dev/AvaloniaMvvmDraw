using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private TranslateTransform? _layerPanelTransform;
        private Point _layerPanelDragStart;
        private bool _layerPanelDragging;

        // Doit cibler le Border nommé x:Name="layerPanel"
        private Border? _layerPanel;

        private void LayerHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            // Utilise le champ généré par XAML
            _layerPanel ??= layerPanel;

            _layerPanelTransform = _layerPanel?.RenderTransform as TranslateTransform;
            if (_layerPanelTransform is null && _layerPanel != null)
            {
                _layerPanelTransform = new TranslateTransform();
                _layerPanel.RenderTransform = _layerPanelTransform;
            }

            _layerPanelDragStart = e.GetPosition(this);
            _layerPanelDragging = true;
            e.Pointer.Capture((IInputElement?)sender);
            e.Handled = true;
        }

        private void LayerHeader_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_layerPanelDragging || _layerPanelTransform is null || e.Pointer.Captured != sender)
                return;

            var p = e.GetPosition(this);
            var dx = p.X - _layerPanelDragStart.X;
            var dy = p.Y - _layerPanelDragStart.Y;

            _layerPanelTransform.X += dx;
            _layerPanelTransform.Y += dy;

            _layerPanelDragStart = p;
            e.Handled = true;
        }

        private void LayerHeader_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Pointer.Captured == sender)
                e.Pointer.Capture(null);

            _layerPanelDragging = false;
            e.Handled = true;
        }

        private void LayerHeader_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _layerPanelDragging = false;
        }
    }
}