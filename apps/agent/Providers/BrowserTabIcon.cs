using System.Security.Cryptography;
using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Providers;

public sealed class BrowserTabIcon(BrowserTabStore tabs)
{
    private string? encoded;
    private ApplicationIcon? icon;

    public ApplicationIcon? Read(string? nativeTabTitle)
    {
        var next = tabs.Read(nativeTabTitle)?.FaviconPng;
        if (next == encoded) return icon;
        encoded = next;
        icon = null;
        if (next is null) return null;
        try
        {
            var bytes = Convert.FromBase64String(next);
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png
                || codec.Info.Width is < 1 or > 128 || codec.Info.Height is < 1 or > 128) return null;
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null) return null;
            // Re-encode a validated bitmap; do not pass arbitrary metadata to the renderer.
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            var pixels = png.ToArray();
            icon = new(Convert.ToHexString(SHA256.HashData(pixels)), pixels);
        }
        catch { /* A missing or corrupt favicon falls back to Chrome's executable icon. */ }
        return icon;
    }
}
