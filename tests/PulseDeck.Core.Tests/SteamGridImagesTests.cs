using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class SteamGridImagesTests
{
    [Fact]
    public void MatchesOnlyUniqueExactTitles()
    {
        using var json = JsonDocument.Parse("""{"success":true,"data":[{"id":1,"name":"Battlefield 6"},{"id":2,"name":"Battlefield 2042"}]}""");
        Assert.Equal(1, SteamGridImages.MatchGame(json.RootElement, "BATTLEFIELD™ 6"));
        Assert.Null(SteamGridImages.MatchGame(json.RootElement, "Battlefield"));
        using var duplicate = JsonDocument.Parse("""{"success":true,"data":[{"id":1,"name":"FC 27"},{"id":2,"name":"FC 27"}]}""");
        Assert.Null(SteamGridImages.MatchGame(duplicate.RootElement, "FC 27"));
    }
    [Fact]
    public void ImagesRequireTrustedHostsAndSuitableDimensions()
    {
        using var json = JsonDocument.Parse("""
        {"success":true,"data":[
            {"url":"https://cdn2.steamgriddb.com/hero/good.png","width":1920,"height":620},
            {"url":"https://cdn2.steamgriddb.com.evil.test/hero/bad.png","width":1920,"height":620},
            {"url":"https://cdn2.steamgriddb.com/grid/portrait.png","width":600,"height":900},
            {"url":"https://cdn2.steamgriddb.com/hero/oversized.png","width":8000,"height":2000},
            {"url":"https://cdn2.steamgriddb.com/hero/excluded.png","width":1920,"height":620,"humor":true}
        ]}
        """);
        Assert.Single(SteamGridImages.Candidates(json.RootElement, true));
        Assert.EndsWith("good.png", SteamGridImages.Candidates(json.RootElement, true)[0].AbsoluteUri);
        Assert.False(SteamGridImages.SafeImageUrl(new("http://cdn2.steamgriddb.com/image.png")));
        Assert.False(SteamGridImages.SafeImageUrl(new("https://secret@cdn2.steamgriddb.com/image.png")));
    }
    [Fact]
    public void ManualBackgroundWinsAndAutomaticBackgroundNeverLeaksOutsideGaming()
    {
        var config = new DeckConfig { BackgroundPath = "general.png", GameThemes = [new("bf6", "manual.png")] };
        var state = new DeckState(DateTimeOffset.UtcNow, "gaming", "bf6", new([], "connected"), new(false,"","","",0,0,"idle"), new([],null,"idle"), new(false,"",null,"idle"))
            { Game = new(42,"bf6","Battlefield 6",true,null,"available"), GameArtwork = new("available","cover.png","SteamGridDB") { BackgroundPath = "hero.png" } };
        Assert.Equal("manual.png", GameTheme.BackgroundFor(state, config));
        Assert.Equal("hero.png", GameTheme.BackgroundFor(state, config with { GameThemes = [] }));
        Assert.Equal("general.png", GameTheme.BackgroundFor(state with { Profile = "desktop" }, config));
        Assert.Equal("general.png", GameTheme.BackgroundFor(state, config with { GameProcesses = [] }));
    }
}
