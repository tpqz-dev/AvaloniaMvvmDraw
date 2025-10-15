using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaMvvmDraw.Views.Interfaces;
using Serilog;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private Point? _dragStartPoint;
        private IDrawableLayer? _draggedLayer;
        private int _dragOriginalIndex = -1;
        private bool _isReorderActive;
        private const double DragThreshold = 4; // pixels

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
            layersList.SelectedItem = _draggedLayer;
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

            var targetIndex = GetIndexFromPoint(pos);
            if (targetIndex < 0)
                return;

            var currentIndex = drawingSurface.Layers.IndexOf(_draggedLayer);
            if (currentIndex >= 0 && targetIndex != currentIndex)
            {
                drawingSurface.Layers.Move(currentIndex, targetIndex);
                Log.Debug("[Reorder] Move {Layer} {From}->{To}", _draggedLayer.Name, currentIndex, targetIndex);
            }
            e.Handled = true;
        }

        // Appelé par XAML : PointerReleased="LayersList_PointerReleased"
        private void LayersList_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Pointer.Captured == layersList)
                e.Pointer.Capture(null);
            if (_isReorderActive)
            {
                Log.Debug("[Reorder] End drag layer={Layer}", _draggedLayer?.Name);
            }
            ClearReorderState();
        }

        private int GetIndexFromPoint(Point pt)
        {
            int index = 0;
            foreach (var item in layersList.GetVisualDescendants().OfType<ListBoxItem>())
            {
                if (item.DataContext is not IDrawableLayer) continue;
                var bounds = item.Bounds;
                var origin = item.TranslatePoint(new Point(0, 0), layersList) ?? new Point(bounds.X, bounds.Y);
                var midY = origin.Y + bounds.Height / 2;
                if (pt.Y < midY)
                    return index;
                index++;
            }
            // Si on passe en dessous de tous les items, placer à la fin
            return drawingSurface.Layers.Count - 1;
        }

        private void ClearReorderState()
        {
            _dragStartPoint = null;
            _draggedLayer = null;
            _dragOriginalIndex = -1;
            _isReorderActive = false;
        }

        // Les anciens handlers DragOver / Drop (OS) ne sont plus nécessaires mais peuvent rester vides.
        private void LayersList_DragOver(object? sender, DragEventArgs e) { }
        private void LayersList_Drop(object? sender, DragEventArgs e) { }
    }
}