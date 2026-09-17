using Windows.Media.Control;
using Windows.Storage.Streams;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class MediaProvider
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? previousSession;
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
            if (!Equals(session, previousSession)) { artwork.Clear(); previousSession = session; }
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
                { ArtworkId = artwork.Current?.Id };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { manager = null; previousSession = null; artwork.Clear(); return new(false, "", "", "", 0, 0, "unavailable"); }
    }
}
