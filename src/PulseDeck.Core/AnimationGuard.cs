namespace PulseDeck.Core;

// One failed animation attempt stays suspended until explicitly toggled or changed.
public sealed class AnimationGuard
{
    private bool suspended;
    private bool previouslyEnabled;
    private string previousPath = "";
    public string Select(bool requested, string path, DisplaySnapshot display)
    {
        if (!requested || !previouslyEnabled || path != previousPath) suspended = false;
        previouslyEnabled = requested;
        previousPath = path;
        if (requested && display.LastTransportError is not null && display.Status is "recovering" or "error") suspended = true;
        return !requested ? "off" : suspended ? "suspended" : "enabled";
    }
}
