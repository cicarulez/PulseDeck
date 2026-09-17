namespace PulseDeck.Core;

public sealed record GameSession(int ProcessId, string ProcessName, DateTimeOffset StartedAt, double ElapsedSeconds);
public sealed record FpsSnapshot(string Status, double? FramesPerSecond = null, double? FrameTimeMs = null,
    int? ProcessId = null, string? Detail = null);
public sealed record GameArtworkSnapshot(string Status = "unavailable", string? Path = null, string? Source = null);

public static class GameSessions
{
    // Identity includes process start time: reusing a PID must never continue the old session.
    public static GameSession FromProcess(int pid, string name, DateTimeOffset started, DateTimeOffset now) =>
        new(pid, name, started, Math.Max(0, (now - started).TotalSeconds));
}
