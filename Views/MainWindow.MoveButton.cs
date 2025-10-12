using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;

namespace AvaloniaMvvmDraw.Views
{
    public partial class MainWindow
    {
        private bool _isMoveMode;
        private const string MoveIconUri = "avares://AvaloniaMvvmDraw/Assets/move_icon.png";

        private void MoveButton_Click(object? sender, RoutedEventArgs e)
        {
            var surface = this.FindControl<DrawingSurface>("drawingSurface");
            var icon = this.FindControl<Image>("moveButtonIcon");

            if (surface is null || icon is null)
                return;

            _isMoveMode = !_isMoveMode;

            //if (_isMoveMode)
            //{
                surface.BeginMoveLastLayer();
                icon.Source = new Bitmap(AssetLoader.Open(new Uri(MoveIconUri)));
            //}
            //else
            //{
            //    surface.CancelMoveMode();
            //    // Pas d'ArrowIconUri : on garde la même icône
            //    icon.Source = new Bitmap(AssetLoader.Open(new Uri(MoveIconUri)));
            //}
        }
    }
}