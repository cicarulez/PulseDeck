using PulseDeck.Core;
using Xunit;

public class MediaDetailsTests
{
    [Theory]
    [InlineData("Album", 2024, "First, Second - Album (2024)")]
    [InlineData("Album", null, "First, Second - Album")]
    [InlineData("", 2024, "First, Second")]
    [InlineData(" ", null, "First, Second")]
    public void DetailsOmitMissingAlbumOrYear(string album, int? year, string expected)
    {
        var media = new MediaSnapshot(true, "Song", "First", "Spotify.exe", 0, 180, "connected")
            { Artists = ["First", "Second"], Album = album, AlbumYear = year };
        Assert.Equal(expected, media.DisplayDetails);
    }

    [Fact]
    public void AlbumWithoutArtistDoesNotStartWithASeparator()
    {
        var media = new MediaSnapshot(true, "Song", "", "Player", 0, 180, "connected") { Album = "Album" };
        Assert.Equal("Album", media.DisplayDetails);
    }
}
