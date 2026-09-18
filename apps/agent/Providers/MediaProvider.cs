using Windows.Media.Control;
using Windows.Storage.Streams;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class MediaProvider(BrowserMediaStore browser, IHttpClientFactory clients)
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly MediaArtworkCache artwork = new();
    public MediaArtwork? Artwork => artwork.Current;
    public async Task<MediaSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
            var sessions = manager.GetSessions();
            var session = sessions.FirstOrDefault(s => s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)
                && s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? sessions.FirstOrDefault(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? manager.GetCurrentSession();
            var browserMedia = browser.Read();
            if (browserMedia is not null && BrowserMediaSelection.PreferBrowser(browserMedia.Playing,
                session?.SourceAppUserModelId,
                session?.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing))
                return await ReadBrowser(browserMedia, cancellationToken);
            if (session is null) { artwork.Clear(); return new(false, "", "", "", 0, 0, "idle"); }
            var metadata = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
            var identity = System.Text.Json.JsonSerializer.Serialize(new[] { session.SourceAppUserModelId,
                metadata.Title, metadata.Artist, metadata.AlbumTitle, metadata.TrackNumber.ToString() });
            await artwork.RefreshAsync(identity, async token =>
            {
                if (metadata.Thumbnail is null) return null;
                using var stream = await metadata.Thumbnail.OpenReadAsync().AsTask(token);
                if (stream.Size is 0 or > MediaArtwork.MaximumBytes) return null;
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                var size = (uint)stream.Size;
                if (await reader.LoadAsync(size).AsTask(token) != size) return null;
                var bytes = new byte[size];
                reader.ReadBytes(bytes);
                return bytes;
            }, cancellationToken);
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            var position = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
            if (playing) position += Math.Max(0, (DateTimeOffset.Now - timeline.LastUpdatedTime).TotalSeconds) * (playback.PlaybackRate ?? 1);
            if (duration > 0) position = Math.Min(position, duration);
            return new(playing, metadata.Title, metadata.Artist, session.SourceAppUserModelId, position, duration, "connected")
                { ArtworkId = artwork.Current?.Id, Album = metadata.AlbumTitle };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            manager = null;
            if (browser.Read() is { } media) return await ReadBrowser(media, cancellationToken);
            artwork.Clear(); return new(false, "", "", "", 0, 0, "unavailable");
        }
    }

    private async Task<MediaSnapshot> ReadBrowser(BrowserMedia media, CancellationToken cancellationToken)
    {
        await artwork.RefreshAsync("youtube:" + media.VideoId, async token =>
        {
            using var client = clients.CreateClient("youtube-artwork");
            // Validated video ID, fixed HTTPS host/path, no arbitrary browser-supplied URLs.
            return await client.GetByteArrayAsync("https://i.ytimg.com/vi/" + media.VideoId + "/hqdefault.jpg", token);
        }, cancellationToken);
        return new(media.Playing, media.Title, media.Artist, "Chrome · YouTube", media.PositionSeconds,
            media.DurationSeconds, "connected") { ArtworkId = artwork.Current?.Id, Source = "youtube-extension" };
    }
}
