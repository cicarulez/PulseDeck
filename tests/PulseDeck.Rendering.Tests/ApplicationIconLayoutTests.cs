using PulseDeck.Agent.Rendering;
using SkiaSharp;
using Xunit;

public sealed class ApplicationIconLayoutTests
{
    [Fact]
    public void PaddedLogoFillsHeaderWithoutStretching()
    {
        using var logo = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(logo))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(24, 28, 16, 8, paint);
        }
        var bounds = ApplicationIconLayout.VisibleBounds(logo);
        Assert.Equal(SKRect.Create(24, 28, 16, 8), bounds);
        using var output = new SKBitmap(40, 40);
        using var target = new SKCanvas(output);
        target.Clear(SKColors.Transparent);
        ApplicationIconLayout.Draw(target, logo, bounds, SKRect.Create(2, 2, 36, 36));
        Assert.Equal(SKRect.Create(2, 11, 36, 18), ApplicationIconLayout.VisibleBounds(output));
    }

    [Fact]
    public void TransparentLogoHasNoVisibleBounds()
    {
        using var logo = new SKBitmap(64, 64);
        logo.Erase(SKColors.Transparent);
        Assert.True(ApplicationIconLayout.VisibleBounds(logo).IsEmpty);
    }
}
