using System.Collections.Concurrent;

namespace PulseDeck.Core;

// Hold a short real speech event across the display's one-second sampling interval.
public sealed class VoiceActivity
{
    private readonly ConcurrentDictionary<ulong, (bool Active, DateTimeOffset At)> members = new();
    public void Update(ulong id, bool active, DateTimeOffset now)
    {
        if (active) members[id] = (true, now);
        else if (members.TryGetValue(id, out var previous) && previous.Active)
            members.TryUpdate(id, (false, now), previous);
    }
    public bool IsSpeaking(ulong id, DateTimeOffset now) => members.TryGetValue(id, out var value)
        && (value.Active || now - value.At < TimeSpan.FromSeconds(1));
    public void Remove(ulong id) => members.TryRemove(id, out _);
    public void Clear() => members.Clear();
}
