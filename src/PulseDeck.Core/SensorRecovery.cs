namespace PulseDeck.Core;

// Bound retries per outage. Zero RPM is a valid reading, not an outage.
public sealed class SensorRecovery
{
    private DateTimeOffset? missingSince, healthySince;
    private DateTimeOffset nextAttempt;
    public int Attempts { get; private set; }
    public bool Observe(bool required, bool healthy, bool permitted, DateTimeOffset now)
    {
        if (!required) { Attempts = 0; missingSince = healthySince = null; nextAttempt = default; return false; }
        if (healthy)
        {
            missingSince = null; healthySince ??= now;
            if (now - healthySince >= TimeSpan.FromSeconds(60)) { Attempts = 0; nextAttempt = default; }
            return false;
        }
        healthySince = null; missingSince ??= now;
        if (!permitted || Attempts >= 3 || now - missingSince < TimeSpan.FromSeconds(30) || now < nextAttempt) return false;
        Attempts++;
        nextAttempt = now.AddSeconds(Attempts == 1 ? 60 : 120);
        return true;
    }
}
