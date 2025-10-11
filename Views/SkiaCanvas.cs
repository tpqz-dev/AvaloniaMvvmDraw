using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;

namespace AvaloniaMvvmDraw.Views;

public class SkiaCanvas : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var pixelSize = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        var dpi = 96; // ou récupérez la valeur réelle si besoin

        using var skiaBitmap = new SKBitmap(pixelSize.Width, pixelSize.Height);
        using var skiaCanvas = new SKCanvas(skiaBitmap);

        // Dessin SkiaSharp ici
        skiaCanvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = true };
        skiaCanvas.DrawCircle(100, 100, 50, paint);

        // Transfert du rendu Skia vers Avalonia
        using var image = SKImage.FromBitmap(skiaBitmap);
        using var data = image.Encode();
        using var stream = data.AsStream();

        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
        context.DrawImage(
                avaloniaBitmap,
                new Rect(0, 0, avaloniaBitmap.Size.Width, avaloniaBitmap.Size.Height),
                new Rect(0, 0, Bounds.Width, Bounds.Height)
            );
    }
}