using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private bool _dragging;
        private Point _dragStart;
        private double _originX;
        private double _originY;

        // Added declaration of floatingTransform
        private TranslateTransform floatingTransform = new TranslateTransform();

        private void FloatingHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // Sync transform with the one defined in XAML
                var transform = floatingPanel.RenderTransform as TranslateTransform;
                if (transform is null)
                {
                    transform = new TranslateTransform { X = 40, Y = 40 };
                    floatingPanel.RenderTransform = transform;
                }
                floatingTransform = transform;

                _dragging = true;
                _dragStart = e.GetPosition(this);
                _originX = floatingTransform.X;
                _originY = floatingTransform.Y;
                e.Pointer.Capture((IInputElement)sender!);
                e.Handled = true;
            }
        }

        private void FloatingHeader_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging) return;
            var pos = e.GetPosition(this);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;
            floatingTransform.X = _originX + dx;
            floatingTransform.Y = _originY + dy;
            e.Handled = true;
        }

        private void FloatingHeader_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            // Fix: use e.Pointer.Capture(null) to release capture
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        private void FloatingHeader_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _dragging = false;
        }
    }
}