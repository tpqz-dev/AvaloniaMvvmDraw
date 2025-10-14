using System;
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

        private void LayersList_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(layersList);
                Log.Information("Drag start registered at {X}, {Y}", _dragStartPoint?.X, _dragStartPoint?.Y);
            }
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

            Log.Information("Starting drag for layer: {LayerName}", layer.Name);

            var data = new DataObject();
            data.Set("application/x-layer", layer);
            
            try
            {
                var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                Log.Information("DragDrop completed with result: {Result}", result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during DragDrop operation");
            }
            
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
            Log.Information("Drop event triggered - Sender: {Sender}, Source: {Source}", 
                sender?.GetType().Name, e.Source?.GetType().Name);

            if (!e.Data.Contains("application/x-layer"))
            {
                Log.Information("Drop: No layer data found");
                return;
            }

            var obj = e.Data.Get("application/x-layer");
            if (obj is not IDrawableLayer dragged)
            {
                Log.Information("Drop: Invalid layer data");
                return;
            }

            Log.Information("Drop: Processing layer {LayerName}", dragged.Name);

            var layers = drawingSurface.Layers;
            var oldIndex = layers.IndexOf(dragged);
            if (oldIndex < 0) 
            {
                Log.Information("Drop: Layer not found in collection");
                return;
            }

            var sourceControl = e.Source as Control;
            var targetContainer = sourceControl?.FindAncestorOfType<ListBoxItem>();

            int insertIndex;
            if (targetContainer?.DataContext is IDrawableLayer target)
            {
                var targetIndex = drawingSurface.Layers.IndexOf(target);
                var p = e.GetPosition(targetContainer);
                var insertAfter = p.Y > targetContainer.Bounds.Height / 2;
                insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
                Log.Information("Drop: Target found at index {TargetIndex}, insert at {InsertIndex}", targetIndex, insertIndex);
            }
            else
            {
                insertIndex = layers.Count;
                Log.Information("Drop: No target, inserting at end (index {InsertIndex})", insertIndex);
            }

            if (insertIndex > oldIndex) insertIndex--;
            if (insertIndex == oldIndex || insertIndex < 0) 
            {
                Log.Information("Drop: No movement needed (oldIndex: {OldIndex}, insertIndex: {InsertIndex})", oldIndex, insertIndex);
                return;
            }

            Log.Information("Drop: Moving layer from {OldIndex} to {NewIndex}", oldIndex, insertIndex);
            layers.Move(oldIndex, insertIndex);
            drawingSurface.SelectedDrawableLayer = dragged;
            e.Handled = true;
        }
    }
}