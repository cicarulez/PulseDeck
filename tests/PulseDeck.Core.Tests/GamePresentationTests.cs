using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class GamePresentationTests
{
    private static readonly ForegroundSnapshot Game = new(42, "BF6", "Test game", true, "icon", "available");

    [Fact]
    public void ArtworkFollowsDebouncedGameAndDoesNotClaimBrowserAsGame()
    {
        var profiles = new ProfileSelector();
        var scenes = new GameSceneSelector();
        var config = new DeckConfig();
        var now = DateTimeOffset.UnixEpoch;
        ForegroundSnapshot? Tick(ForegroundSnapshot foreground, int second)
        {
            var time = now.AddSeconds(second);
            return scenes.Select(config, profiles.Select(config, foreground.ProcessName, false, time), foreground, time);
        }
        Assert.Null(Tick(Game, 0));
        Assert.Equal(Game, Tick(Game, 3));
        var browser = new ForegroundSnapshot(43, "browser", "Browser", false, "browser-icon", "available");
        Assert.Equal(Game, Tick(browser, 4));
        Assert.Equal(Game, Tick(Game, 5));
        Assert.Equal(Game, Tick(browser, 6));
        Assert.Null(Tick(browser, 9));
        Assert.Null(scenes.Select(config with { ProfileMode = "gaming" }, "gaming", browser, now.AddSeconds(10)));
    }

    [Fact]
    public void GameSwitchAndRuleRemovalNeverKeepPreviousArtwork()
    {
        var scenes = new GameSceneSelector();
        var config = new DeckConfig { GameProcesses = ["bf6", "other"] };
        var now = DateTimeOffset.UnixEpoch;
        Assert.Equal(Game, scenes.Select(config, "gaming", Game, now));
        var other = Game with { ProcessName = "other", IconId = "other-icon" };
        Assert.Equal(other, scenes.Select(config, "gaming", other, now.AddSeconds(1)));
        Assert.Null(scenes.Select(config with { GameProcesses = [] }, "gaming", ForegroundSnapshot.Empty, now.AddSeconds(2)));
    }

    [Fact]
    public void OldSettingsGainEmptyThemesWithoutChangingExistingPreferences()
    {
        var config = JsonSerializer.Deserialize<DeckConfig>("""{"accentColor":"#ffff00","trackedMemberId":"local-test","gameProcesses":["bf6"]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(config.Validate());
        Assert.True(config.GamingLayout);
        Assert.Empty(config.GameThemes);
        Assert.Equal("local-test", config.TrackedMemberId);
        Assert.Equal("#ffff00", config.AccentColor);
    }

    [Fact]
    public void RejectsAmbiguousAndMalformedGameThemes()
    {
        Assert.NotNull((new DeckConfig { GameThemes = [new("bf6", "a.png"), new("BF6.exe", "b.png")] }).Validate());
        Assert.NotNull((new DeckConfig { GameThemes = [new("C:\\Games\\bf6.exe", "a.png")] }).Validate());
        Assert.NotNull((new DeckConfig { GameThemes = [new("bf6", null!)] }).Validate());
        Assert.NotNull((new DeckConfig { GameThemes = [null!] }).Validate());
        Assert.NotNull((new DeckConfig { GameThemes = null! }).Validate());
        Assert.Null((new DeckConfig { GameThemes = [new("BF6.exe", "a.png")] }).Validate());
    }

    [Fact]
    public void HeaderUsesOnlyConfiguredUserAndClearsUnavailableStates()
    {
        var member = new VoiceMember("target", "Test nickname", false, false);
        var other = new VoiceMember("other", "Another user", true, true);
        var snapshot = new DiscordSnapshot([other, member], other, "connected");
        var header = TrackedVoiceHeader.Resolve(snapshot, "target");
        Assert.Equal("Test nickname", header.Name);
        Assert.Equal("MIC ATTIVO", header.Status);
        Assert.False(header.Muted);
        Assert.Equal("MUTE", TrackedVoiceHeader.Resolve(snapshot with { Members = [member with { Mute = true }] }, "target").Status);
        Assert.Equal("DEAF", TrackedVoiceHeader.Resolve(snapshot with { Members = [member with { Deaf = true }] }, "target").Status);
        Assert.Null(TrackedVoiceHeader.Resolve(snapshot with { Status = "offline" }, "target").Muted);
        Assert.Equal("UTENTE FUORI CANALE", TrackedVoiceHeader.Resolve(snapshot with { Members = [other] }, "target").Status);
        Assert.Equal("UTENTE NON SCELTO", TrackedVoiceHeader.Resolve(snapshot, "").Status);
        Assert.Equal("DISCORD", TrackedVoiceHeader.Resolve(snapshot, "missing").Name);
    }
}
