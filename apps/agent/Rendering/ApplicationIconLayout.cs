using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public static class ApplicationIconLayout
{
    // Windows package logos often include generous transparent tile padding.
    public static SKRect VisibleBounds(SKBitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha > 8)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < left ? SKRect.Empty : new(left, top, right + 1, bottom + 1);
    }

    public static void Draw(SKCanvas canvas, SKBitmap bitmap, SKRect source, SKRect target)
    {
        if (source.IsEmpty) return;
        var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
        var width = source.Width * scale; var height = source.Height * scale;
        canvas.DrawBitmap(bitmap, source, SKRect.Create(target.MidX - width / 2, target.MidY - height / 2, width, height));
    }
}
