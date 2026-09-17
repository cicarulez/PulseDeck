using PulseDeck.Agent.Providers;
using SkiaSharp;
using Xunit;

public class ArtworkTests
{
    public static byte[] Picture(SKColor color, int width = 64, int height = 64)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void ArtworkIsBoundedAndBadImagesStayUnavailable()
    {
        Assert.Null(MediaArtwork.Decode([1, 2, 3]));
        Assert.Null(MediaArtwork.Decode(new byte[MediaArtwork.MaximumBytes + 1]));
        Assert.Null(MediaArtwork.Decode(Picture(SKColors.Red, 2100, 2100)));
        var art = MediaArtwork.Decode(Picture(SKColors.Blue, 600, 300));
        Assert.NotNull(art);
        using var decoded = SKBitmap.Decode(art.Png);
        Assert.Equal(256, decoded.Width);
        Assert.Equal(128, decoded.Height);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SameTrackCachesButChangesMissingArtAndSessionEndClearIt()
    {
        var clock = new Clock();
        var cache = new MediaArtworkCache(clock);
        int reads = 0;
        Task<byte[]?> Read(CancellationToken token) { reads++; return Task.FromResult<byte[]?>(Picture(SKColors.Red)); }
        await cache.RefreshAsync("Spotify/A", Read, default);
        var id = cache.Current!.Id;
        await cache.RefreshAsync("Spotify/A", Read, default);
        Assert.Equal(1, reads);
        clock.Now += TimeSpan.FromSeconds(30);
        await cache.RefreshAsync("Spotify/A", Read, default);
        Assert.Equal(2, reads);
        Assert.Equal(id, cache.Current!.Id);
        await cache.RefreshAsync("Spotify/B", _ => Task.FromResult<byte[]?>(null), default);
        Assert.Null(cache.Current);
        await cache.RefreshAsync("Other/A", Read, default);
        Assert.Equal(3, reads);
        cache.Clear();
        Assert.Null(cache.Current);
    }

    [Fact]
    public async Task FailedRefreshClearsOldArtAndRetriesWithoutFailingMetadata()
    {
        var clock = new Clock();
        var cache = new MediaArtworkCache(clock);
        await cache.RefreshAsync("A", _ => Task.FromResult<byte[]?>(Picture(SKColors.Red)), default);
        clock.Now += TimeSpan.FromSeconds(30);
        await cache.RefreshAsync("A", _ => throw new IOException("thumbnail unavailable"), default);
        Assert.Null(cache.Current);
        clock.Now += TimeSpan.FromSeconds(5);
        await cache.RefreshAsync("A", _ => Task.FromResult<byte[]?>(Picture(SKColors.Blue)), default);
        Assert.NotNull(cache.Current);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.RefreshAsync("B",
            token => Task.FromCanceled<byte[]?>(token), new CancellationToken(true)));
        Assert.Null(cache.Current);
    }
}
