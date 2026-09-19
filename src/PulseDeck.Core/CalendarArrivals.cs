namespace PulseDeck.Core;

// Identities cover the whole feed, not just the seven-day display window.
// HasFutureEvent selects actionable additions without treating an edited title,
// a moved occurrence or an old event entering the display window as a new event.
public sealed record CalendarIdentity(string Id, bool HasFutureEvent);

public sealed class CalendarArrivalTracker
{
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private int revision = -1;
    private DateTimeOffset? fetchedAt;
    private bool initialized;
    public int Observe(CalendarSnapshot snapshot, int sourceRevision, bool notify)
    {
        if (sourceRevision != revision)
        {
            revision = sourceRevision; seen.Clear(); fetchedAt = null; initialized = false;
        }
        if (snapshot.Status is not ("connected" or "empty") || snapshot.FetchedAt == fetchedAt) return 0;
        fetchedAt = snapshot.FetchedAt;
        if (snapshot.Identities is not { } identities) return 0;
        var added = 0;
        foreach (var identity in identities)
            if (seen.Add(identity.Id) && identity.HasFutureEvent) added++;
        var result = initialized && notify ? added : 0;
        initialized = true;
        // Retain tombstones through transient omissions; keep memory bounded even
        // for a long-running calendar with many additions. Reset quietly at the cap.
        if (seen.Count > 30000)
        {
            seen.Clear(); foreach (var identity in identities) seen.Add(identity.Id);
        }
        return result;
    }
}
