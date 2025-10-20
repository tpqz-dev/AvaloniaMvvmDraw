using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Linq;
using Serilog;
using AvaloniaMvvmDraw.Views.Models;
using AvaloniaMvvmDraw.Views.Interfaces;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _overviewTimer;

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

            // Selection change guard during drag (handler defined in LayersDragDrop.cs)
            layersList.SelectionChanged += LayersList_SelectionChanged;

            // Drag/drop (kept no-op now but enabled if needed)
            layersList.AddHandler(DragDrop.DragOverEvent, LayersList_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            layersList.AddHandler(DragDrop.DropEvent, LayersList_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            DragDrop.SetAllowDrop(layersList, true);
        }

        private void ResetLayerRotation_Click(object? sender, RoutedEventArgs e)
        {
            if (drawingSurface?.SelectedDrawableLayer is Views.Models.ImageLayer img)
            {
                img.Rotation = 0.0;
                Log.Information("Reset rotation for layer {Name}", img.Name);
                drawingSurface.InvalidateVisual();
                UpdateOverviewSnapshot();
            }
        }

        private void ResetLayerWidth_Click(object? sender, RoutedEventArgs e)
        {
            if (drawingSurface?.SelectedDrawableLayer is Views.Models.ImageLayer img && img.Bitmap is not null)
            {
                var old = img.Rect;
                img.Rect = new Rect(old.X, old.Y, img.ImageWidth, old.Height);
                Log.Information("Reset width for layer {Name} to {W}", img.Name, img.ImageWidth);
                drawingSurface.InvalidateVisual();
                UpdateOverviewSnapshot();
            }
        }

        private void ResetLayerHeight_Click(object? sender, RoutedEventArgs e)
        {
            if (drawingSurface?.SelectedDrawableLayer is Views.Models.ImageLayer img && img.Bitmap is not null)
            {
                var old = img.Rect;
                img.Rect = new Rect(old.X, old.Y, old.Width, img.ImageHeight);
                Log.Information("Reset height for layer {Name} to {H}", img.Name, img.ImageHeight);
                drawingSurface.InvalidateVisual();
                UpdateOverviewSnapshot();
            }
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
            UpdateOverviewSnapshot();
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
            UpdateOverviewSnapshot();
        }

        private void BindLayersReverse()
        {
            if (layersList is null || drawingSurface is null) return;
            layersList.ItemsSource = drawingSurface.Layers.Reverse().ToList();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            BindLayersReverse();
            drawingSurface.Layers.CollectionChanged += (_, __) => BindLayersReverse();

            // Overview snapshot updates (periodic + on size changes)
            _overviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _overviewTimer.Tick += (_, __) => UpdateOverviewSnapshot();
            _overviewTimer.Start();
            drawingSurface.PropertyChanged += (_, __) => UpdateOverviewSnapshot();
            SizeChanged += (_, __) => UpdateOverviewSnapshot();
            UpdateOverviewSnapshot();
        }

        private void UpdateOverviewSnapshot()
        {
            try
            {
                if (overviewImage is null || drawingSurface is null) return;
                var w = Math.Max(1, (int)Math.Ceiling(drawingSurface.Bounds.Width));
                var h = Math.Max(1, (int)Math.Ceiling(drawingSurface.Bounds.Height));
                if (w == 0 || h == 0) return;
                var rtb = new RenderTargetBitmap(new PixelSize(w, h));
                rtb.Render(drawingSurface);
                overviewImage.Source = rtb;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to snapshot DrawingSurface for Overview");
            }
        }

        private void ResizeToAspectCurrentMaxSide_Click(object? sender, RoutedEventArgs e)
        {
            if (drawingSurface.SelectedDrawableLayer is not IMovableRectLayer m)
                return;
            if (drawingSurface.SelectedDrawableLayer is not ImageLayer img || img.OriginalAspectRatio <= 0)
                return;

            var rc = m.Rect;
            var center = new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2);
            var currentMax = Math.Max(rc.Width, rc.Height);

            double newW, newH;
            if (img.OriginalAspectRatio >= 1.0)
            {
                // Landscape or square: width = currentMax
                newW = currentMax;
                newH = newW / img.OriginalAspectRatio;
            }
            else
            {
                // Portrait: height = currentMax
                newH = currentMax;
                newW = newH * img.OriginalAspectRatio;
            }

            m.Rect = new Rect(center.X - newW / 2, center.Y - newH / 2, newW, newH);
            Log.Information("Resize to saved aspect by current max side: {W}x{H} (ratio {R:0.###}, currentMax {M})", newW, newH, img.OriginalAspectRatio, currentMax);
            drawingSurface.InvalidateVisual();
            UpdateOverviewSnapshot();
        }
    }
}