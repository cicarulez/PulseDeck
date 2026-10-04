using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public static class AudioSpectrumPanel
{
    public static void Draw(SKCanvas canvas, AudioSpectrumSnapshot audio, float x, float y, float width,
        SKColor[] palette, SKTypeface typeface)
    {
        using var card = new SKPaint { Color = new SKColor(12, 24, 28, 200), IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(x, y, width, 208), 8, 8, card);
        using var font = new SKFont(typeface, 13);
        using var label = new SKPaint { Color = new SKColor(144, 162, 161), IsAntialias = true };
        canvas.DrawText("SPETTRO · SOLO SPOTIFY", x + 16, y + 24, SKTextAlign.Left, font, label);
        if (audio.Status != "connected" || audio.Bands is not { Length: AudioSpectrumAnalyzer.BandCount } bands)
        {
            var message = audio.Status switch { "paused" => "Riproduzione in pausa", "connecting" => "Collegamento al player…", _ => "Spettro non disponibile" };
            canvas.DrawText(message, x + 16, y + 110, SKTextAlign.Left, font, label);
            return;
        }
        using var shader = AuraPalette.Shader(palette, x + 16, width - 32);
        using var bar = new SKPaint { Color = palette[0], Shader = shader, IsAntialias = true };
        var stride = (width - 32) / bands.Length;
        for (int i = 0; i < bands.Length; i++)
        {
            var height = Math.Clamp(float.IsFinite(bands[i]) ? bands[i] : 0, 0, 1) * 140;
            if (height > .5f) canvas.DrawRoundRect(SKRect.Create(x + 16 + i * stride, y + 180 - height, stride - 4, height), 2, 2, bar);
        }
        canvas.DrawText("BASSI", x + 16, y + 199, SKTextAlign.Left, font, label);
        canvas.DrawText("ALTI", x + width - 46, y + 199, SKTextAlign.Left, font, label);
    }
}
