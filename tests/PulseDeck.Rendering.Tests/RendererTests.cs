using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;
using SkiaSharp;
using Xunit;

public class RendererTests
{
    private static readonly DeckState State = new(DateTimeOffset.UnixEpoch, "desktop", "",
        new([new("cpu.load", "CPU", 50, "%")], "connected"), new(true, "Synthetic test track", "Test artist", "Test", 5, 10, "connected"),
        new([], null, "connected"), new(false, "COM5", null, "disconnected"));

    [Fact]
    public void SixteenthWidgetChangesItsOwnCardAndClassicKeepsItHidden()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig();
        var empty = renderer.Render(State, config);
        config.Widgets[15] = new("extra8", "metric", "cpu.load", "", "", "LAST", Style: "ring");
        var full = renderer.Render(State, config);
        Assert.Equal(1920 * 480 * 4, full.Pixels.Length);
        Assert.False(empty.Pixels.AsSpan().SequenceEqual(full.Pixels));
        // Last row/column only: upper rows and media remain byte-for-byte identical.
        Assert.True(empty.Pixels.AsSpan(0, 1920 * 350 * 4).SequenceEqual(full.Pixels.AsSpan(0, 1920 * 350 * 4)));
        var classic = renderer.Render(State, config with { Layout = "classic" });
        config.Widgets[15] = config.Widgets[15] with { Source = "none" };
        Assert.Equal(classic.Pixels, renderer.Render(State, config with { Layout = "classic" }).Pixels);
    }

    [Fact]
    public void CoverRequiresMatchingCurrentMediaAndIsClearedWhenSessionEnds()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig();
        var art = MediaArtwork.Decode(ArtworkTests.Picture(SKColors.Red))!;
        var baseline = renderer.Render(State, config);
        Assert.Equal(baseline.Pixels, renderer.Render(State, config, art).Pixels);
        var playing = State with { Media = State.Media with { ArtworkId = art.Id } };
        Assert.NotEqual(baseline.Pixels, renderer.Render(playing, config, art).Pixels);
        Assert.Equal(baseline.Pixels, renderer.Render(State, config).Pixels);
    }

    [Fact]
    public void LegacyGifUsesOnlyFirstFrameAndRemovingItClearsTheCache()
    {
        // Two synthetic 1x1 frames: old files remain readable, never animated.
        var gif = Convert.FromHexString("47494638396101000100800000FF00000000FF"
            + "21F904000A0000002C0000000001000100000202440100"
            + "21F904000A0000002C00000000010001000002024C01003B");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
        try
        {
            File.WriteAllBytes(path, gif);
            using var background = new StaticBackground();
            Assert.Equal(SKColors.Red, background.Get(path)!.GetPixel(0, 0));
            Assert.Equal(1, background.Count);
            Assert.Equal("static", background.Status);
            Assert.Same(background.Get(path), background.Get(path));
            Assert.Null(background.Get(""));
            Assert.Equal("none", background.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ActiveApplicationIconMustMatchCurrentSnapshotAndClearsOnExit()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig();
        var icon = new ApplicationIcon("test-icon", ArtworkTests.Picture(SKColors.Magenta));
        var active = State with { Foreground = new(42, "testgame", "Synthetic game", true, "test-icon", "available") };
        var without = renderer.Render(active, config);
        var withIcon = renderer.Render(active, config, applicationIcon: icon);
        Assert.False(without.Pixels.AsSpan().SequenceEqual(withIcon.Pixels));
        Assert.Equal(without.Pixels, renderer.Render(active, config, applicationIcon: icon with { Id = "other-app" }).Pixels);
        var exited = State with { Foreground = ForegroundSnapshot.Empty };
        Assert.Equal(renderer.Render(exited, config).Pixels, renderer.Render(exited, config, applicationIcon: icon).Pixels);
    }

    [Fact]
    public void InvalidOrDeletedBackgroundDoesNotBreakRendering()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
        try
        {
            File.WriteAllText(path, "invalid test image");
            using var renderer = new DeckRenderer();
            var config = new DeckConfig { BackgroundPath = path };
            Assert.NotEmpty(renderer.Render(State, config).Png);
            Assert.Equal("unavailable", renderer.BackgroundStatus);
            File.Delete(path);
            Assert.NotEmpty(renderer.Render(State, config).Png);
            Assert.Equal("unavailable", renderer.BackgroundStatus);
        }
        finally { File.Delete(path); }
    }
}
