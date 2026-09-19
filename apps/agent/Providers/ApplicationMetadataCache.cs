namespace PulseDeck.Agent.Providers;

public sealed record ApplicationMetadata(string? Name, ApplicationIcon? Icon);

/// <summary>Called by the foreground reader. One asynchronous lookup at a time;
/// slow Windows metadata must never block display updates or grow a work queue.</summary>
public sealed class ApplicationMetadataCache(Func<string, CancellationToken, Task<ApplicationMetadata?>> load,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, (ApplicationMetadata? Value, DateTimeOffset Expires)> cache = new(StringComparer.Ordinal);
    private (string Id, Task<ApplicationMetadata?> Task)? pending;

    public bool IsLoading(string id) => !cache.ContainsKey(id) || pending?.Id == id;

    public ApplicationMetadata? Read(string id)
    {
        if (pending is { } job && job.Task.IsCompleted)
        {
            ApplicationMetadata? value = null;
            if (job.Task.IsCompletedSuccessfully) value = job.Task.Result;
            else _ = job.Task.Exception; // Observe faults; metadata is optional.
            if (!cache.ContainsKey(job.Id) && cache.Count >= 32) cache.Remove(cache.Keys.First());
            if (cache.TryGetValue(job.Id, out var previous) && previous.Value is { } prior)
                value = new(value?.Name ?? prior.Name, value?.Icon ?? prior.Icon);
            cache[job.Id] = (value, time.GetUtcNow().AddSeconds(value?.Icon is null ? 10 : 60));
            pending = null;
        }
        if (cache.TryGetValue(id, out var entry) && time.GetUtcNow() < entry.Expires) return entry.Value;
        if (pending is null)
        {
            pending = (id, Task.Run(async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { return await load(id, timeout.Token).ConfigureAwait(false); }
                catch { return null; }
            }));
        }
        // During a refresh only this application's prior result may be reused.
        return cache.TryGetValue(id, out entry) ? entry.Value : null;
    }
}
