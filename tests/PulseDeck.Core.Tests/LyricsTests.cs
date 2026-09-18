using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class LyricsTests
{
    private static MediaSnapshot Track => new(true, "Synthetic track", "Synthetic artist", "Spotify.exe", 0, 180, "connected");
    [Theory]
    [InlineData("Spotify.exe", "windows", true)]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "windows", true)]
    [InlineData("Chrome · YouTube", "youtube-extension", false)]
    [InlineData("Spotify.exe", "youtube-extension", false)]
    [InlineData("NotSpotify.exe", "windows", false)]
    public void RequiresSpotifySessionIdentity(string app, string source, bool expected) => Assert.Equal(expected, SpotifyLyrics.Eligible(Track with { App = app, Source = source }));
    [Fact]
    public void SeeksBackwardsPausesAndBlankInstrumentalGapsUseActualPosition()
    {
        var lines = SpotifyLyrics.ParseLrc("[ar:Synthetic]\n[00:01.20][00:10.200]Original test line\n[00:04.00]\n[00:06]Another invented line", 180);
        var state = new LyricsSnapshot("synced", "test", lines);
        Assert.Equal(-1, state.CurrentLine(0));
        Assert.Equal("Original test line", lines[state.CurrentLine(1.2)].Text);
        Assert.Equal("", lines[state.CurrentLine(5)].Text);
        Assert.Equal(3, state.CurrentLine(100));
        Assert.Equal(0, state.CurrentLine(2));
        Assert.Equal(0, state.CurrentLine(2));
    }
    [Fact]
    public void AdvancesDisplayBy350MillisecondsWithoutChangingSourceTimestamps()
    {
        var lines = SpotifyLyrics.ParseLrc("[offset:1000]\n[00:02]Invented first line\n[00:05]\n[00:07]Invented last line", 180);
        var state = new LyricsSnapshot("synced", "test", lines);
        Assert.Equal(-1, state.CurrentLine(0.649));
        Assert.Equal(0, state.CurrentLine(0.650));
        Assert.Equal(0, state.CurrentLine(3.649));
        Assert.Equal(1, state.CurrentLine(3.650)); // Instrumental gaps follow the same advance.
        Assert.Equal(2, state.CurrentLine(5.650));
        Assert.Equal(0, state.CurrentLine(2)); // Seeking backwards selects the earlier line.
        Assert.Equal(0, state.CurrentLine(2)); // A paused position remains stable.
        Assert.Equal(new double[] { 1, 4, 6 }, lines.Select(line => line.Seconds));
        Assert.Equal(-1, state.CurrentLine(double.NaN));
        Assert.Equal(-1, state.CurrentLine(double.PositiveInfinity));
    }
    [Fact]
    public void ValidatesTrackIdentityDurationAndInstrumentals()
    {
        using var json = JsonDocument.Parse("""{"trackName":"Synthetic track","artistName":"Synthetic artist","duration":180,"syncedLyrics":"[00:01]Words invented for this test"}""");
        Assert.Equal("synced", SpotifyLyrics.Parse(json.RootElement, Track).Status);
        Assert.Equal("not-found", SpotifyLyrics.Parse(json.RootElement, Track with { DurationSeconds = 190 }).Status);
        Assert.Equal("not-found", SpotifyLyrics.Parse(json.RootElement, Track with { Artist = "Another artist" }).Status);
        using var instrumental = JsonDocument.Parse("""{"trackName":"Synthetic track","artistName":"Synthetic artist","duration":180,"instrumental":true}""");
        Assert.Equal("instrumental", SpotifyLyrics.Parse(instrumental.RootElement, Track).Status);
    }
    [Fact]
    public void RejectsStaleLyricsAndDoesNotReplaceGamingOrOtherPlayers()
    {
        var state = new DeckState(DateTimeOffset.UtcNow,"music","",new([],"idle"),Track,new([],null,"idle"),new(false,"",null,"idle"))
            { Lyrics = new("synced", SpotifyLyrics.Key(Track), [new(0,"Original test")]) };
        Assert.True(SpotifyLyrics.Show(state, new()));
        Assert.False(SpotifyLyrics.Show(state with { Media = Track with { Title = "Next song" } }, new()));
        Assert.False(SpotifyLyrics.Show(state with { Profile = "gaming" }, new()));
        Assert.False(SpotifyLyrics.Show(state, new() { SpotifyLyrics = false }));
        Assert.False(SpotifyLyrics.Show(state with { Media = Track with { App = "chrome.exe" } }, new()));
    }
    [Fact]
    public void HandlesOffsetsMalformedAndOversizedInput()
    {
        Assert.Equal(1, SpotifyLyrics.ParseLrc("[offset:1000]\n[00:02]Invented line", 180).Single().Seconds);
        Assert.Empty(SpotifyLyrics.ParseLrc("[00:99]Bad\n[999:00]Too late", 180));
        Assert.Empty(SpotifyLyrics.ParseLrc(new string('x',64001), 180));
    }
}
