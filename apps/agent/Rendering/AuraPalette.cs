using SkiaSharp;
using PulseDeck.Core;

namespace PulseDeck.Agent.Rendering;

public static class AuraPalette
{
    public static SKColor[] Colors(DeckConfig config, AuraSnapshot aura)
    {
        var fallback = SKColor.Parse(AuraColor.Accent(config, aura));
        if (!config.AuraEnabled || aura.Status != "connected" || aura.Colors is not { Length: > 0 and <= 64 } colors)
            return [fallback];
        var palette = new SKColor[colors.Length];
        for (var i = 0; i < colors.Length; i++)
            if (!SKColor.TryParse(colors[i], out palette[i])) return [fallback];
        return palette;
    }

    public static SKColor At(SKColor[] colors, float x) =>
        colors[Math.Clamp((int)(x / 1920 * colors.Length), 0, colors.Length - 1)];

    // Stretch the received sequence across one text or music progress bar.
    // The service supplies the animation; interpolation only smooths its colors.
    public static SKShader? Shader(SKColor[] colors, float x, float width, bool readableText = false)
    {
        if (colors.Length == 1 || width <= 0) return null;
        var stops = new float[colors.Length];
        var values = new SKColor[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            stops[i] = (float)i / (colors.Length - 1);
            values[i] = readableText ? Text(colors[i]) : colors[i];
        }
        return SKShader.CreateLinearGradient(new(x, 0), new(x + width, 0), values, stops, SKShaderTileMode.Clamp);
    }

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
