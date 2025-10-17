using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Media;
using AvaloniaMvvmDraw.Views.Interfaces;
using Serilog;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private Point? _dragStartPoint;
        private IDrawableLayer? _draggedLayer;
        private int _dragOriginalIndex = -1; // index in underlying collection
        private bool _isReorderActive;
        private const double DragThreshold = 4; // pixels
        private int _dragTargetSlot = -1; // visual slot index [0..N]

        private void LayersList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isReorderActive && _draggedLayer is not null)
            {
                // Keep selection on dragged layer during drag
                layersList.SelectedItem = _draggedLayer;
                drawingSurface.SelectedDrawableLayer = _draggedLayer;
                Log.Debug("[Reorder] Block selection change during drag");
            }
        }

        // Appelé par XAML : PointerPressed="LayersList_PointerPressed"
        private void LayersList_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(layersList).Properties.IsLeftButtonPressed)
                return;

            var container = (e.Source as Control)?.FindAncestorOfType<ListBoxItem>();
            _draggedLayer = container?.DataContext as IDrawableLayer;
            if (_draggedLayer == null)
                return;

            _dragStartPoint = e.GetPosition(layersList);
            _dragOriginalIndex = drawingSurface.Layers.IndexOf(_draggedLayer);
            _isReorderActive = false;
            _dragTargetSlot = -1;
            HideDropIndicator();
            // Ensure selection is on the dragged item as we begin
            layersList.SelectedItem = _draggedLayer;
            drawingSurface.SelectedDrawableLayer = _draggedLayer;
            e.Pointer.Capture(layersList);
            Log.Debug("[Reorder] Press layer={Layer} index={Index}", _draggedLayer.Name, _dragOriginalIndex);
        }

        // Appelé par XAML : PointerMoved="LayersList_PointerMoved"
        private void LayersList_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStartPoint is null || _draggedLayer is null || _dragOriginalIndex < 0)
                return;
            if (e.Pointer.Captured != layersList)
                return;
            if (!e.GetCurrentPoint(layersList).Properties.IsLeftButtonPressed)
                return; // bouton relâché

            var pos = e.GetPosition(layersList);
            var dy = Math.Abs(pos.Y - _dragStartPoint.Value.Y);
            if (!_isReorderActive && dy < DragThreshold)
                return; // pas encore assez bougé

            if (!_isReorderActive)
            {
                _isReorderActive = true;
                Log.Debug("[Reorder] Start drag layer={Layer}", _draggedLayer.Name);
            }

            // Preview only: compute target slot (0..N) and show indicator line
            if (TryComputeDropSlot(pos, out var slotIndex, out var lineYInHost))
            {
                _dragTargetSlot = slotIndex;
                ShowDropIndicator(lineYInHost);
            }
            else
            {
                _dragTargetSlot = -1;
                HideDropIndicator();
            }
            e.Handled = true;
        }

        // Appelé par XAML : PointerReleased="LayersList_PointerReleased"
        private void LayersList_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Pointer.Captured == layersList)
                e.Pointer.Capture(null);
            if (_draggedLayer is not null)
            {
                // Apply reordering on drop
                if (_isReorderActive && _dragTargetSlot >= 0)
                {
                    var currentIndex = drawingSurface.Layers.IndexOf(_draggedLayer); // underlying index
                    var n = drawingSurface.Layers.Count;
                    // Convert visual slot [0..N] to underlying target index [0..N-1]
                    // visual 0 (avant premier)  -> underlying N-1
                    // visual N (après dernier)  -> underlying 0
                    // visual k                   -> underlying N - k - 1, clamp to [0..N-1]
                    var targetIndex = Math.Clamp(n - _dragTargetSlot - 1, 0, n - 1);
                    if (currentIndex >= 0 && targetIndex != currentIndex)
                    {
                        drawingSurface.Layers.Move(currentIndex, targetIndex);
                        Log.Debug("[Reorder] Drop {Layer} {From}->{To} (slot={Slot},n={N})", _draggedLayer.Name, currentIndex, targetIndex, _dragTargetSlot, n);
                    }
                }
                // Ensure final selection is the dragged layer
                drawingSurface.SelectedDrawableLayer = _draggedLayer;
                layersList.SelectedItem = _draggedLayer;
            }
            if (_isReorderActive)
            {
                Log.Debug("[Reorder] End drag layer={Layer}", _draggedLayer?.Name);
            }
            HideDropIndicator();
            ClearReorderState();
        }

        // Calcule un slot visuel 0..N, et la position Y de la ligne dans l'hôte
        private bool TryComputeDropSlot(Point ptInList, out int slotIndex, out double lineYInHost)
        {
            slotIndex = -1;
            lineYInHost = 0;
            var items = layersList.GetVisualDescendants().OfType<ListBoxItem>().ToList();
            var n = items.Count;
            if (n == 0)
                return false;

            var hostOrigin = layersList.TranslatePoint(new Point(0, 0), layersListHost) ?? new Point(0, 0);

            for (int v = 0; v < n; v++)
            {
                var item = items[v];
                if (item.DataContext is not IDrawableLayer) continue;
                var bounds = item.Bounds;
                var originInList = item.TranslatePoint(new Point(0, 0), layersList) ?? new Point(bounds.X, bounds.Y);
                var top = originInList.Y;
                var mid = top + bounds.Height / 2;
                if (ptInList.Y < mid)
                {
                    slotIndex = v; // before item v
                    lineYInHost = hostOrigin.Y + top;
                    return true;
                }
            }

            // After last item
            var last = items[n - 1];
            var lastOrigin = last.TranslatePoint(new Point(0, 0), layersList) ?? new Point(last.Bounds.X, last.Bounds.Y);
            slotIndex = n; // after last
            lineYInHost = hostOrigin.Y + lastOrigin.Y + last.Bounds.Height;
            return true;
        }

        private void ShowDropIndicator(double yInHost)
        {
            if (dropIndicator is null) return;
            dropIndicator.IsVisible = true;
            dropIndicator.RenderTransform = new TranslateTransform(0, yInHost);
        }

        private void HideDropIndicator()
        {
            if (dropIndicator is null) return;
            dropIndicator.IsVisible = false;
        }

        private void ClearReorderState()
        {
            _dragStartPoint = null;
            _draggedLayer = null;
            _dragOriginalIndex = -1;
            _isReorderActive = false;
            _dragTargetSlot = -1;
        }

        private void LayersList_DragOver(object? sender, DragEventArgs e) { }
        private void LayersList_Drop(object? sender, DragEventArgs e) { }
    }
}