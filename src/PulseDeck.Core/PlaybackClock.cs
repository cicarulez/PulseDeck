namespace PulseDeck.Core;

public static class PlaybackClock
{
    // Extrapolate only the current provider sample; new samples reset seeks and
    // track changes. Bound stale extrapolation instead of accumulating drift.
    public static MediaSnapshot Advance(MediaSnapshot media, double seconds)
    {
        if (!media.Playing || media.Status != "connected" || !double.IsFinite(media.PositionSeconds)
            || !double.IsFinite(seconds) || seconds <= 0) return media;
        var rate = double.IsFinite(media.PlaybackRate) && media.PlaybackRate > 0 ? media.PlaybackRate : 1;
        var position = Math.Max(0, media.PositionSeconds) + Math.Min(seconds, 2) * rate;
        if (double.IsFinite(media.DurationSeconds) && media.DurationSeconds > 0) position = Math.Min(position, media.DurationSeconds);
        return media with { PositionSeconds = position };
    }
}
