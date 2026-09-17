using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;
using SkiaSharp;
using Xunit;

public class RendererTests
{
    [Fact]
    public void WeatherUsesTwelveCardsAndClearsUnavailableReadings()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig { Layout = "weather", WeatherLocation = new("Synthetic test city", 0, 0) };
        var baseline = renderer.Render(State, config);
        config.Widgets[15] = new("extra8", "metric", "cpu.load", "", "", "HIDDEN");
        Assert.Equal(baseline.Pixels, renderer.Render(State, config).Pixels);
        config.Widgets[11] = new("extra4", "metric", "cpu.load", "", "", "LAST");
        Assert.NotEqual(baseline.Pixels, renderer.Render(State, config).Pixels);
        var weather = new WeatherSnapshot { Status = "connected", LocationName = "Synthetic test city", Temperature = 22, Code = 0, IsDay = true };
        var live = renderer.Render(State with { Weather = weather }, config);
        Assert.NotEqual(live.Pixels, renderer.Render(State, config).Pixels);
        Assert.Equal(renderer.Render(State, config).Pixels, renderer.Render(State with { Weather = weather with { Status = "unavailable" } }, config).Pixels);
        Assert.Equal(renderer.Render(State, config).Pixels, renderer.Render(State with { Weather = weather with { LocationName = "Different city" } }, config).Pixels);
        // Switching profiles keeps the weather column and all twelve sensor positions.
        var gaming = renderer.Render(State with { Profile = "gaming", Weather = weather }, config);
        for (var y = 86; y < 442; y++) Assert.True(live.Pixels.AsSpan(y * 1920 * 4, 1340 * 4).SequenceEqual(gaming.Pixels.AsSpan(y * 1920 * 4, 1340 * 4)));
    }
    private static readonly DeckState State = new(DateTimeOffset.UnixEpoch, "desktop", "",
        new([new("cpu.load", "CPU", 50, "%")], "connected"), new(true, "Synthetic test track", "Test artist", "Test", 5, 10, "connected"),
        new([], null, "connected"), new(false, "COM5", null, "disconnected"));

    [Fact]
    public void GamingHasDedicatedLayoutPreservesLastSlotAndCanUseBaseLayout()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig();
        var gaming = State with { Profile = "gaming" };
        var original = renderer.Render(gaming, config with { GamingLayout = false });
        var dedicated = renderer.Render(gaming, config);
        Assert.NotEqual(original.Pixels, dedicated.Pixels);
        config.Widgets[15] = new("extra8", "metric", "cpu.load", "", "", "LAST", Style: "ring");
        var last = renderer.Render(gaming, config);
        Assert.NotEqual(dedicated.Pixels, last.Pixels);
        Assert.True(dedicated.Pixels.AsSpan(0, 1920 * 350 * 4).SequenceEqual(last.Pixels.AsSpan(0, 1920 * 350 * 4)));
        Assert.Equal(renderer.Render(State, config).Pixels, renderer.Render(State, config with { GamingLayout = false }).Pixels);
    }

    [Fact]
    public void GameBackgroundSwitchesAndClearsWithoutLeakingIntoOtherProfiles()
    {
        var first = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        var second = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            File.WriteAllBytes(first, ArtworkTests.Picture(SKColors.Red));
            File.WriteAllBytes(second, ArtworkTests.Picture(SKColors.Blue));
            using var renderer = new DeckRenderer();
            var config = new DeckConfig { GameProcesses = ["bf6", "other"], GameThemes = [new("BF6.exe", first), new("other", second)] };
            var game = new ForegroundSnapshot(42, "bf6", "Synthetic game", true, null, "unavailable");
            var gaming = State with { Profile = "gaming", Game = game };
            var baseline = renderer.Render(State, config);
            var red = renderer.Render(gaming, config);
            Assert.Equal("static", renderer.BackgroundStatus);
            var blue = renderer.Render(gaming with { Game = game with { ProcessName = "other" } }, config);
            Assert.NotEqual(red.Pixels, blue.Pixels);
            Assert.Equal(baseline.Pixels, renderer.Render(State, config).Pixels);
            Assert.Equal("none", renderer.BackgroundStatus);
            renderer.Render(gaming with { Game = game with { ProcessName = "bf6-launcher" } }, config);
            Assert.Equal("none", renderer.BackgroundStatus);
            File.Delete(first);
            Assert.NotEmpty(renderer.Render(gaming, config).Png);
            Assert.Equal("unavailable", renderer.BackgroundStatus);
            Assert.Equal(baseline.Pixels, renderer.Render(State, config).Pixels);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public void VoiceHeaderUpdatesMuteAndKeepsClockClear()
    {
        using var renderer = new DeckRenderer();
        var user = new VoiceMember("target", "A very long synthetic nickname that must fit before the clock", false, false);
        var config = new DeckConfig { TrackedMemberId = "target" };
        var live = State with { Discord = new([user], user, "connected") };
        var active = renderer.Render(live, config);
        var muted = renderer.Render(live with { Discord = new([user with { Mute = true }], null, "connected") }, config);
        // Header crops change and clock pixels stay intact even for an overlong nickname.
        Assert.False(active.Pixels.AsSpan(0, 1920 * 65 * 4).SequenceEqual(muted.Pixels.AsSpan(0, 1920 * 65 * 4)));
        for (var y = 0; y < 65; y++)
            Assert.True(active.Pixels.AsSpan((y * 1920 + 1750) * 4, 170 * 4).SequenceEqual(muted.Pixels.AsSpan((y * 1920 + 1750) * 4, 170 * 4)));
        var offline = live with { Discord = live.Discord with { Status = "offline" } };
        Assert.Equal(renderer.Render(offline with { Discord = new([], null, "offline") }, config).Pixels, renderer.Render(offline, config).Pixels);
    }

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
