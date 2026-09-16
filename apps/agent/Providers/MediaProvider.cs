using Windows.Media.Control;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class MediaProvider
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
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
            if (session is null) return new(false, "", "", "", 0, 0, "idle");
            var metadata = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            var position = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
            if (playing) position += Math.Max(0, (DateTimeOffset.Now - timeline.LastUpdatedTime).TotalSeconds) * (playback.PlaybackRate ?? 1);
            if (duration > 0) position = Math.Min(position, duration);
            return new(playing, metadata.Title, metadata.Artist, session.SourceAppUserModelId, position, duration, "connected");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { manager = null; return new(false, "", "", "", 0, 0, "unavailable"); }
    }
}
