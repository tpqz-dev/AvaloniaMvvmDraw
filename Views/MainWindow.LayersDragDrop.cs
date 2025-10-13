using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaMvvmDraw.Views.Interfaces;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private Point? _dragStartPoint;

        private void LayersList_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                _dragStartPoint = e.GetPosition(layersList);
        }

        private async void LayersList_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStartPoint is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var pos = e.GetPosition(layersList);
            if (Math.Abs(pos.X - _dragStartPoint.Value.X) < 4 &&
                Math.Abs(pos.Y - _dragStartPoint.Value.Y) < 4)
                return;

            var container = (e.Source as Control)?.FindAncestorOfType<ListBoxItem>();
            if (container?.DataContext is not IDrawableLayer layer)
                return;

            var data = new DataObject();
            data.Set("application/x-layer", layer);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            _dragStartPoint = null;
        }

        private void LayersList_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragStartPoint = null;
        }

        private void LayersList_DragOver(object? sender, DragEventArgs e)
        {
            if (!e.Data.Contains("application/x-layer"))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void LayersList_Drop(object? sender, DragEventArgs e)
        {
            if (!e.Data.Contains("application/x-layer"))
                return;

            var obj = e.Data.Get("application/x-layer");
            if (obj is not IDrawableLayer dragged)
                return;

            var layers = drawingSurface.Layers;
            var oldIndex = layers.IndexOf(dragged);
            if (oldIndex < 0) return;

            var sourceControl = e.Source as Control;
            var targetContainer = sourceControl?.FindAncestorOfType<ListBoxItem>();

            int insertIndex;
            if (targetContainer?.DataContext is IDrawableLayer target)
            {
                var targetIndex = drawingSurface.Layers.IndexOf(target);
                var p = e.GetPosition(targetContainer);
                var insertAfter = p.Y > targetContainer.Bounds.Height / 2;
                insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
            }
            else
            {
                // Drop en dehors d'un item => fin de liste
                insertIndex = layers.Count;
            }

            if (insertIndex > oldIndex) insertIndex--; // ajuster si on se déplace vers le bas
            if (insertIndex == oldIndex || insertIndex < 0) return;

            layers.Move(oldIndex, insertIndex);
            drawingSurface.SelectedDrawableLayer = dragged;
            e.Handled = true;
        }
    }
}