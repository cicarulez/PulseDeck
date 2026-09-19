namespace PulseDeck.Core;

public sealed record NotificationOptions
{
    public bool GmailEnabled { get; init; } = true;
    public bool Animate { get; init; } = true;
    public int PollSeconds { get; init; } = 30;
    public string? Validate() => PollSeconds is < 15 or > 300 ? "Il controllo Gmail deve essere tra 15 e 300 secondi." : null;
}

// Connectors publish authoritative counts independently from arrival events.
// No message subjects, bodies or account identifiers enter display state.
public sealed record NotificationSource(string Id, string Kind, string Status, int? UnreadCount = null, DateTimeOffset? UpdatedAt = null);
public sealed record NotificationEvent(string SourceId, string Kind, string Title, string Caption);
public sealed record NotificationVisual(NotificationSource[] Sources, NotificationEvent? Arrival = null, float Seconds = 4, bool IsTest = false);

public sealed class NotificationCenter(TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<string, NotificationSource> sources = new();
    private NotificationEvent? arrival;
    private long started;
    private readonly Dictionary<string, long> lastArrivals = new(StringComparer.Ordinal);
    private long? testStarted;
    private string testKind = "mail";
    public long AnimationsStarted { get; private set; }
    public void Publish(NotificationSource source, NotificationEvent? incoming = null, bool animate = true, double groupSeconds = 10)
    {
        lock (gate)
        {
            sources[source.Id] = source;
            if (source.Status != "connected" && arrival?.SourceId == source.Id) arrival = null;
            if (incoming is null || !animate || source.Status != "connected") return;
            // A burst extends its grouping window, never restarts or queues the animation.
            var coalesced = lastArrivals.TryGetValue(source.Id, out var previous) && time.GetElapsedTime(previous).TotalSeconds < groupSeconds;
            var lastArrival = time.GetTimestamp(); lastArrivals[source.Id] = lastArrival;
            if (coalesced) return;
            arrival = incoming; started = lastArrival; AnimationsStarted++;
        }
    }
    public void StartTest(string kind = "mail") { lock (gate) { testKind = kind; testStarted = time.GetTimestamp(); } }
    public NotificationVisual Read(bool animate = true)
    {
        lock (gate)
        {
            if (testStarted is { } test)
            {
                var elapsed = (float)time.GetElapsedTime(test).TotalSeconds;
                if (elapsed < 8 && animate)
                {
                    if (testKind == "calendar") return new(sources.Values.ToArray(),
                        elapsed < 3.2f ? new("test-calendar", "calendar", "Nuovo evento nel calendario", "PROVA · EVENTO SIMULATO") : null,
                        Math.Min(elapsed, 4), true);
                    return new([new("test", "mail", "connected", elapsed < 1 ? 1 : elapsed < 5 ? 3 : 0)],
                        new("test", "mail", "Prova notifica", "3 EMAIL SIMULATE · NESSUN ACCESSO GMAIL"), Math.Min(elapsed, 4), true);
                }
                testStarted = null;
            }
            var seconds = (float)time.GetElapsedTime(started).TotalSeconds;
            if (!animate || seconds >= 3.2f) arrival = null;
            return new(sources.Values.OrderBy(s => s.Id).ToArray(), arrival, arrival is null ? 4 : seconds);
        }
    }
}
