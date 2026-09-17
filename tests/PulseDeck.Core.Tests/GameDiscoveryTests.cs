using PulseDeck.Core;
using Xunit;

public class GameDiscoveryTests
{
    [Fact]
    public void ReadsEscapedSteamLibrariesAndUnicodeTitles()
    {
        const string vdf = "\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Program Files (x86)\\\\Steam\" } \"1\" { \"path\" \"E:\\\\SteamLibrary\" } }";
        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"E:\SteamLibrary" }, GameDiscoveryRules.Values(vdf, "path"));
        Assert.Equal("Borderlands® 4", GameDiscoveryRules.Values("\"AppState\" { \"name\" \"Borderlands® 4\" }", "name").Single());
    }
    [Theory]
    [InlineData(@"EAAntiCheat.GameServiceLauncher.exe")]
    [InlineData(@"Engine\Binaries\Win64\UnrealGame.exe")]
    [InlineData(@"__Installer\touchup.exe")]
    [InlineData(@"Game\Binaries\Win64\CrashReportClient.exe")]
    public void DoesNotClassifySupportingExecutables(string executable) => Assert.True(GameDiscoveryRules.IsHelper(executable));
    [Fact]
    public void ConservativeTitleMatchingLeavesUnrelatedExecutablesForReview()
    {
        Assert.True(GameDiscoveryRules.MatchesTitle("F1® 25", "F1_25.exe", ""));
        Assert.True(GameDiscoveryRules.MatchesTitle("Cyberpunk 2077", @"bin\x64\Cyberpunk2077.exe", ""));
        Assert.True(GameDiscoveryRules.MatchesTitle("Borderlands® 4", "Borderlands4-Win64-Shipping.exe", ""));
        Assert.False(GameDiscoveryRules.MatchesTitle("ARC Raiders", "PioneerGame.exe", "PioneerGame"));
        Assert.False(GameDiscoveryRules.IsHelper(@"Binaries\Win64\RocketLeague.exe"));
    }
    [Fact]
    public void ResolvesUnrealChildOnlyInsideBinariesWithDeclaredName()
    {
        string[] declared = [@"C:\Games\ARC Raiders\PioneerGame.exe"];
        Assert.True(GameDiscoveryRules.IsDeclaredUnrealTarget(@"PioneerGame\Binaries\Win64\PioneerGame.exe", declared));
        Assert.False(GameDiscoveryRules.IsDeclaredUnrealTarget(@"tools\PioneerGame.exe", declared));
        Assert.False(GameDiscoveryRules.IsDeclaredUnrealTarget(@"PioneerGame\Binaries\Win64\Other.exe", declared));
    }
    [Fact]
    public void IgnoreWinsOverAutomaticAndManualConfirmation()
    {
        var game = new DiscoveredGame("Game", @"D:\Games\Game\game.exe", @"D:\Games\Game", "Steam", "123", true);
        Assert.Equal("recognized", game.Status(new()));
        Assert.Equal("review", (game with { Automatic = false }).Status(new()));
        Assert.Equal("recognized", (game with { Automatic = false }).Status(new() { ConfirmedExecutables = [game.Executable.ToUpperInvariant()] }));
        Assert.Equal("ignored", game.Status(new() { ConfirmedExecutables = [game.Executable], IgnoredExecutables = [game.Executable] }));
    }
    [Fact]
    public void ForegroundPathDecisionOverridesDiscoveredNameCollision()
    {
        var config = new DeckConfig { ProfileDelaySeconds = 0, GameProcesses = ["game"] };
        var selector = new ProfileSelector();
        Assert.Equal("desktop", selector.Select(config, "game", false, DateTimeOffset.UtcNow, recognizedGame: false));
        Assert.Equal("gaming", selector.Select(config, "game", false, DateTimeOffset.UtcNow, recognizedGame: true));
    }
    [Theory]
    [InlineData(@"\\server\games")]
    [InlineData("relative")]
    [InlineData("https://example.test/games")]
    public void RejectsNonLocalLibraryPaths(string folder) => Assert.NotNull(new GameDiscoveryOptions { Folders = [folder] }.Validate());
}
