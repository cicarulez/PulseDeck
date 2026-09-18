using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;
using SkiaSharp;
using Xunit;

public class RendererTests
{
    [Theory]
    [InlineData("compact")]
    [InlineData("weather")]
    [InlineData("classic")]
    public void LeavingDiscordRestoresEmptyMediaEvenWhenOthersRemain(string layout)
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig { Layout = layout, TrackedMemberId = "me" };
        var me = new VoiceMember("me", "Tracked participant", false, false);
        var other = new VoiceMember("other", "Other participant", false, false);
        var state = State with { Media = new(false, "", "", "", 0, 0, "idle"), Discord = new([], null, "connected") };
        byte[] MediaArea(DeckState snapshot, DeckConfig settings) => renderer.Render(snapshot, settings).Pixels
            .Skip(1920 * 90 * 4).Take(1920 * 200 * 4).Chunk(1920 * 4)
            .SelectMany(row => row.Skip(1410 * 4).Take(465 * 4)).ToArray();
        var empty = MediaArea(state, config);
        var inside = state with { Discord = new([me, other], me, "connected") };
        Assert.NotEqual(empty, MediaArea(inside, config));
        // A stale Tracked object must not keep the roster expanded after departure.
        Assert.Equal(empty, MediaArea(state with { Discord = new([other], me, "connected") }, config));
        Assert.Equal(empty, MediaArea(inside with { Discord = inside.Discord with { Status = "offline" } }, config));
        Assert.Equal(empty, MediaArea(state, config with { TrackedMemberId = "" }));
        Assert.NotEqual(empty, MediaArea(state with { Discord = new([other], null, "connected") }, config with { TrackedMemberId = "" }));
        Assert.NotEqual(empty, MediaArea(inside, config)); // Returning expands again.
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("weather")]
    [InlineData("classic")]
    public void DesktopUsesMediaSpaceForEightDiscordMembersAndRestoresPausedMedia(string layout)
    {
        using var renderer = new DeckRenderer();
        var members = Enumerable.Range(0, 8).Select(i => new VoiceMember(i.ToString(), "Player " + i, false, false)).ToArray();
        var config = new DeckConfig { Layout = layout };
        var empty = State with { Media = new(false, "", "", "", 0, 0, "idle"), Discord = new(members, null, "connected") };
        var expanded = renderer.Render(empty, config);
        members[7] = members[7] with { Streaming = true };
        var streaming = renderer.Render(empty, config);
        Assert.NotEqual(expanded.Pixels, streaming.Pixels); // Eighth participant is visible.
        for (var y = 0; y < 480; y++)
            Assert.True(expanded.Pixels.AsSpan(y * 1920 * 4, 1352 * 4).SequenceEqual(streaming.Pixels.AsSpan(y * 1920 * 4, 1352 * 4)));
        var paused = empty with { Media = State.Media with { Playing = false } };
        var pausedFrame = renderer.Render(paused, config);
        members[7] = members[7] with { Streaming = false };
        Assert.Equal(pausedFrame.Pixels, renderer.Render(paused, config).Pixels); // Small roster is restored.
        Assert.NotEqual(expanded.Pixels, renderer.Render(paused, config).Pixels);
        Assert.Equal(expanded.Pixels, renderer.Render(empty, config).Pixels);
    }

    [Theory]
    [InlineData("desktop", "compact")]
    [InlineData("desktop", "classic")]
    [InlineData("gaming", "weather")]
    [InlineData("music", "compact")]
    public void StreamingIndicatorIsIndependentOfVoiceAvailabilityAndMute(string profile, string layout)
    {
        using var renderer = new DeckRenderer();
        var member = new VoiceMember("member", "Synthetic participant with a long name", true, false);
        var state = State with { Profile = profile, Discord = new([member], null, "connected") { SpeakingStatus = "unavailable" } };
        var config = new DeckConfig { Layout = layout };
        var baseline = renderer.Render(state, config);
        var streaming = renderer.Render(state with { Discord = state.Discord with { Members = [member with { Streaming = true }] } }, config);
        Assert.NotEqual(baseline.Pixels, streaming.Pixels);
        Assert.Equal(baseline.Pixels, renderer.Render(state with { Discord = state.Discord with { Members = [member with { Streaming = false }] } }, config).Pixels);
    }

    [Fact]
    public void MusicPlaybackStatusChangesLyricsHeaderWithoutChangingCoverColumn()
    {
        using var renderer = new DeckRenderer();
        var media = new MediaSnapshot(true, "Synthetic track", "Artist", "Spotify.exe", 2, 180, "connected");
        var state = State with { Profile = "music", Media = media, Lyrics = new("synced", SpotifyLyrics.Key(media), [new(0, "Original fixture line")]) };
        var playing = renderer.Render(state, new());
        var paused = renderer.Render(state with { Media = media with { Playing = false } }, new());
        for (var y = 90; y < 440; y++)
            Assert.True(playing.Pixels.AsSpan((y * 1920 + 32) * 4, 322 * 4).SequenceEqual(paused.Pixels.AsSpan((y * 1920 + 32) * 4, 322 * 4)));
        Assert.Contains(Enumerable.Range(90, 30), y =>
            !playing.Pixels.AsSpan((y * 1920 + 388) * 4, 920 * 4).SequenceEqual(paused.Pixels.AsSpan((y * 1920 + 388) * 4, 920 * 4)));
    }
    [Fact]
    public void MusicRamShowsOnlyPercentageWhileDesktopKeepsCapacity()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig { Layout = "weather" };
        config.Widgets[2] = new("bar3", "metric", "ram.load", "", "", "RAM", Style: "ring");
        config.Widgets[7] = config.Widgets[7] with { Source = "none" };
        var media = new MediaSnapshot(true, "Synthetic track", "Artist", "Spotify.exe", 2, 180, "connected");
        var hardware = new HardwareSnapshot([new("ram.load", "RAM", 50, "%"), new("ram.total", "Total", 64, "GiB"), new("ram.used", "Used", 32, "GiB")], "connected");
        var otherHardware = new HardwareSnapshot([new("ram.load", "RAM", 50, "%"), new("ram.total", "Total", 32, "GiB"), new("ram.used", "Used", 16, "GiB")], "connected");
        var state = State with { Profile = "music", Media = media, Hardware = hardware, Lyrics = new("loading", SpotifyLyrics.Key(media)) };
        Assert.Equal(renderer.Render(state, config).Pixels, renderer.Render(state with { Hardware = otherHardware }, config).Pixels);
        Assert.NotEqual(renderer.Render(state with { Profile = "desktop" }, config).Pixels,
            renderer.Render(state with { Profile = "desktop", Hardware = otherHardware }, config).Pixels);
    }
    [Fact]
    public void MusicUsesThreeRowsOfThreeSensorCards()
    {
        using var renderer = new DeckRenderer();
        var media = new MediaSnapshot(true, "Synthetic track", "Artist", "Spotify.exe", 2, 180, "connected");
        var state = State with { Profile = "music", Media = media, Lyrics = new("loading", SpotifyLyrics.Key(media)) };
        var config = new DeckConfig();
        for (var i = 0; i < 9; i++)
            config.Widgets[i] = new(WidgetCatalog.Slots[i].Id, "metric", "cpu.load", "", "", "CPU", Style: i < 3 ? "ring" : "value");
        var baseline = renderer.Render(state, config);
        for (var i = 0; i < 9; i++)
        {
            var original = config.Widgets[i];
            config.Widgets[i] = original with { Source = "none" };
            var hidden = renderer.Render(state, config);
            var x = 1364 + i % 3 * 178;
            var y = 100 + i / 3 * 106;
            Assert.NotEqual(baseline.Pixels, hidden.Pixels);
            for (var row = 0; row < 480; row++)
            {
                if (row < y || row >= y + 80)
                    Assert.True(baseline.Pixels.AsSpan(row * 1920 * 4, 1920 * 4).SequenceEqual(hidden.Pixels.AsSpan(row * 1920 * 4, 1920 * 4)));
                else
                {
                    Assert.True(baseline.Pixels.AsSpan(row * 1920 * 4, x * 4).SequenceEqual(hidden.Pixels.AsSpan(row * 1920 * 4, x * 4)));
                    Assert.True(baseline.Pixels.AsSpan((row * 1920 + x + 168) * 4, (1920 - x - 168) * 4).SequenceEqual(hidden.Pixels.AsSpan((row * 1920 + x + 168) * 4, (1920 - x - 168) * 4)));
                }
            }
            config.Widgets[i] = original;
        }
        config.Widgets[9] = new(WidgetCatalog.Slots[9].Id, "metric", "cpu.load", "", "", "HIDDEN");
        Assert.Equal(baseline.Pixels, renderer.Render(state, config).Pixels);
    }
    [Fact]
    public void TrackLoadingKeepsMusicGeometryAndGapNeverDisplaysOldLyrics()
    {
        using var r = new DeckRenderer(); var c = new DeckConfig {Layout="weather"};
        var media = new MediaSnapshot(true,"Synthetic track","Artist","Spotify.exe",2,180,"connected");
        var synced = State with {Profile="music", Media=media, Lyrics=new("synced",SpotifyLyrics.Key(media),[new(0,"Original synthetic line")])};
        var a=r.Render(synced,c); var b=r.Render(synced with {Lyrics=new("loading",SpotifyLyrics.Key(media))},c);
        for(var y=86;y<440;y++) {
            Assert.True(a.Pixels.AsSpan((y*1920+32)*4,320*4).SequenceEqual(b.Pixels.AsSpan((y*1920+32)*4,320*4)));
            Assert.True(a.Pixels.AsSpan((y*1920+1364)*4,524*4).SequenceEqual(b.Pixels.AsSpan((y*1920+1364)*4,524*4)));
        }
        var gap=synced with {SpotifyTransition=true,Media=new(false,"","","",0,0,"idle")};
        Assert.Equal(r.Render(gap,c).Pixels,r.Render(gap with {Lyrics=new()},c).Pixels);
    }
    [Fact]
    public void SpotifyLyricsAreIsolatedAndDiscordHeaderDisappearsOutsideRoster()
    {
        using var renderer = new DeckRenderer();
        var media = new MediaSnapshot(true, "Synthetic track", "Synthetic artist", "Spotify.exe", 3, 180, "connected");
        var lyrics = new LyricsSnapshot("synced", SpotifyLyrics.Key(media), [new(1,"Invented opening line"), new(4,"Invented second line")]);
        var config = new DeckConfig { Layout = "weather", TrackedMemberId = "me" };
        var state = State with { Profile = "music", Media = media };
        var baseline = renderer.Render(state, config);
        var withLyrics = renderer.Render(state with { Lyrics = lyrics }, config);
        Assert.NotEqual(baseline.Pixels, withLyrics.Pixels);
        Assert.Equal(renderer.Render(state, config with { SpotifyLyrics = false }).Pixels,
            renderer.Render(state with { Lyrics = lyrics }, config with { SpotifyLyrics = false }).Pixels);
        var other = state with { Media = media with { App = "chrome.exe" } };
        Assert.Equal(renderer.Render(other, config).Pixels, renderer.Render(other with { Lyrics = lyrics }, config).Pixels);
        var stale = state with { Media = media with { Title = "Another track" } };
        Assert.Equal(renderer.Render(stale, config).Pixels, renderer.Render(stale with { Lyrics = lyrics }, config).Pixels);
        var inside = state with { Discord = new([new("me","Tester",false,false)],null,"connected") };
        var on = renderer.Render(inside, config);
        Assert.NotEqual(baseline.Pixels, on.Pixels);
        var outside = renderer.Render(state with { Discord = new([],new("me","Stale",false,false),"connected") }, config);
        for (var y = 10; y < 56; y++) Assert.True(baseline.Pixels.AsSpan((y*1920+1180)*4,340*4).SequenceEqual(outside.Pixels.AsSpan((y*1920+1180)*4,340*4)));
        var advanced = renderer.Render(state with { Lyrics = lyrics, Media = media with { PositionSeconds = 5 } }, config);
        Assert.NotEqual(withLyrics.Pixels, advanced.Pixels);
    }
    [Theory]
    [InlineData("weather")]
    [InlineData("compact")]
    [InlineData("classic")]
    public void NewsChangesOnlyFooterAndDisablingRestoresIt(string layout)
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig { Layout = layout };
        var baseline = renderer.Render(State, config);
        var news = new NewsSnapshot { Status = "connected", FetchedAt = State.Timestamp,
            Items = [new("Synthetic source", "Synthetic test headline " + new string('X', 250), "https://example.com/news", null)] };
        var on = config with { News = new() { Enabled = true } };
        var active = renderer.Render(State with { News = news }, on);
        Assert.True(baseline.Pixels.AsSpan(0, 1920 * 446 * 4).SequenceEqual(active.Pixels.AsSpan(0, 1920 * 446 * 4)));
        Assert.NotEqual(baseline.Pixels, active.Pixels);
        Assert.Equal(baseline.Pixels, renderer.Render(State with { News = news }, config).Pixels);
        Assert.NotEqual(active.Pixels, renderer.Render(State, on).Pixels);
        foreach (var size in new[] { 20, 24, 26 })
        {
            var sized = renderer.Render(State with { News = news }, on with { News = on.News with { FontSize = size } });
            Assert.True(baseline.Pixels.AsSpan(0, 1920 * 446 * 4).SequenceEqual(sized.Pixels.AsSpan(0, 1920 * 446 * 4)));
        }
    }
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
        // Gaming replaces the side panels while preserving all twelve sensor positions.
        var gaming = renderer.Render(State with { Profile = "gaming", Weather = weather }, config);
        for (var y = 86; y < 442; y++) Assert.True(live.Pixels.AsSpan((y * 1920 + 380) * 4, 960 * 4).SequenceEqual(gaming.Pixels.AsSpan((y * 1920 + 380) * 4, 960 * 4)));
        Assert.NotEqual(live.Pixels, gaming.Pixels);
        var fixedLayout = renderer.Render(State with { Profile = "gaming", Weather = weather }, config with { GamingLayout = false });
        for (var y = 86; y < 442; y++) Assert.True(live.Pixels.AsSpan(y * 1920 * 4, 1920 * 4).SequenceEqual(fixedLayout.Pixels.AsSpan(y * 1920 * 4, 1920 * 4)));
    }
    [Fact]
    public void SpeakingChangesDesktopAndGamingRostersAndUnavailableClearsHighlight()
    {
        using var renderer = new DeckRenderer();
        var members = Enumerable.Range(0, 8).Select(i => new VoiceMember(i.ToString(), "Player " + i, false, false)).ToArray();
        var state = State with { Discord = new(members, null, "connected") { SpeakingStatus = "connected" } };
        var config = new DeckConfig { Layout = "weather" };
        var desktop = renderer.Render(state, config);
        var gaming = renderer.Render(state with { Profile = "gaming" }, config);
        members[7] = members[7] with { Speaking = true };
        Assert.NotEqual(desktop.Pixels, renderer.Render(state, config).Pixels);
        var speaking = renderer.Render(state with { Profile = "gaming" }, config);
        Assert.NotEqual(gaming.Pixels, speaking.Pixels);
        for (var y = 86; y < 442; y++) Assert.True(gaming.Pixels.AsSpan((y * 1920 + 380) * 4, 1520 * 4).SequenceEqual(speaking.Pixels.AsSpan((y * 1920 + 380) * 4, 1520 * 4)));
        var unavailable = state with { Profile = "gaming", Discord = state.Discord with { SpeakingStatus = "unavailable" } };
        var stale = renderer.Render(unavailable, config);
        members[7] = members[7] with { Speaking = null };
        Assert.Equal(stale.Pixels, renderer.Render(unavailable, config).Pixels);
    }

    [Fact]
    public void VolumeChangesOnlyItsHeaderRegionAndMuteIsExplicit()
    {
        using var renderer = new DeckRenderer();
        var config = new DeckConfig();
        var initial = renderer.Render(State with { Volume = new("connected", 35) }, config);
        var muted = renderer.Render(State with { Volume = new("connected", 35, true) }, config);
        Assert.NotEqual(initial.Pixels, muted.Pixels);
        for (var y = 0; y < 480; y++)
        {
            Assert.True(initial.Pixels.AsSpan(y * 1920 * 4, 1540 * 4).SequenceEqual(muted.Pixels.AsSpan(y * 1920 * 4, 1540 * 4)));
            Assert.True(initial.Pixels.AsSpan((y * 1920 + 1740) * 4, 180 * 4).SequenceEqual(muted.Pixels.AsSpan((y * 1920 + 1740) * 4, 180 * 4)));
        }
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
