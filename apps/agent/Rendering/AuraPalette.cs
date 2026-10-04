using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public static class AuraPalette
{
    // Keep the received color on bars/rings; lighten only text on the dark panel.
    public static SKColor Text(SKColor color)
    {
        static double Linear(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        static double Luminance(SKColor c) => .2126 * Linear(c.Red) + .7152 * Linear(c.Green) + .0722 * Linear(c.Blue);
        if (Luminance(color) >= .35) return color;
        double low = 0, high = 1;
        SKColor Blend(double amount) => new((byte)Math.Round(color.Red + (255 - color.Red) * amount),
            (byte)Math.Round(color.Green + (255 - color.Green) * amount),
            (byte)Math.Round(color.Blue + (255 - color.Blue) * amount));
        for (var i = 0; i < 10; i++)
        {
            var mid = (low + high) / 2;
            if (Luminance(Blend(mid)) < .35) low = mid; else high = mid;
        }
        return Blend(high);
    }
}
