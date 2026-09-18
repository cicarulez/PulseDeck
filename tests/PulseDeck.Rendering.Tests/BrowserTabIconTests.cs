using PulseDeck.Agent.Providers;
using PulseDeck.Core;
using SkiaSharp;
using Xunit;

public sealed class BrowserTabIconTests
{
    private static string Png(SKColor color)
    {
        using var bitmap = new SKBitmap(32, 32); bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(png.ToArray());
    }
    [Fact]
    public void TabSwitchAndMissingIconNeverReusePreviousFavicon()
    {
        var store = new BrowserTabStore(); var provider = new BrowserTabIcon(store);
        store.Update(new("First", Png(SKColors.Red)));
        var first = provider.Read("First"); Assert.NotNull(first);
        Assert.Same(first, provider.Read("First"));
        Assert.Null(provider.Read("Second"));
        store.Update(new("Second", Png(SKColors.Green)));
        var second = provider.Read("Second"); Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        using var bitmap = SKBitmap.Decode(second.Png);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(0, 0));
        store.Update(new("Second", null));
        Assert.Null(provider.Read("Second"));
        store.Update(null);
        Assert.Null(provider.Read("First"));
    }
    [Fact]
    public void CorruptPngFallsBackWithoutThrowing()
    {
        var store = new BrowserTabStore(); var provider = new BrowserTabIcon(store);
        var bytes = Convert.FromBase64String(Png(SKColors.Blue));
        Assert.True(store.Update(new("Page", Convert.ToBase64String(bytes[..33]))));
        Assert.Null(provider.Read("Page"));
        Assert.Null(provider.Read("Page"));
    }
}
