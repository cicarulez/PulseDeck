namespace PulseDeck.Core;

public sealed record DeckConfig
{
    public int SchemaVersion { get; init; } = 1;
    public string ProfileMode { get; init; } = "auto";
    public string[] GameProcesses { get; init; } = ["bf6", "Battlefield"];
    public int ProfileDelaySeconds { get; init; } = 3;
    public string DiscordMode { get; init; } = "embedded";
    public string DiscordBaseUrl { get; init; } = "http://127.0.0.1:5090";
    public string TrackedMemberId { get; init; } = "";
    public string DisplayPort { get; init; } = "COM5";
    public string BackgroundPath { get; init; } = "";
    public string AccentColor { get; init; } = "#a9ff69";
    public string Layout { get; init; } = "compact";
    public WeatherLocation? WeatherLocation { get; init; }
    public NewsOptions News { get; init; } = new();
    public bool GamingLayout { get; init; } = true;
    public GameTheme[] GameThemes { get; init; } = [];

    public WidgetConfig[] Widgets { get; init; } = WidgetCatalog.Defaults();

    public string? Validate()
    {
        if (News is null) return "Invalid news settings.";
        if (News.Validate() is { } newsError) return newsError;
        if (SchemaVersion != 1) return "Unsupported configuration version.";
        if (Layout is not ("classic" or "compact" or "weather")) return "Unknown display layout.";
        if (WeatherLocation is { } location && !location.IsValid) return "Invalid weather location.";
        if (ProfileMode is not ("auto" or "desktop" or "gaming" or "music")) return "Unknown profile.";
        if (ProfileDelaySeconds is < 0 or > 30) return "Profile delay must be between 0 and 30 seconds.";
        if (GameProcesses is null || GameProcesses.Length > 50 || GameProcesses.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 100)) return "Invalid game process list.";
        if (DiscordMode is not ("embedded" or "external")) return "Unknown Discord mode.";
        if (!Uri.TryCreate(DiscordBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return "Enter an HTTP(S) Discord backend URL without credentials.";
        if (DisplayPort is null || !System.Text.RegularExpressions.Regex.IsMatch(DisplayPort, @"^COM[1-9]\d{0,3}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Invalid COM port.";
        if (AccentColor is null || !System.Text.RegularExpressions.Regex.IsMatch(AccentColor, "^#[0-9a-fA-F]{6}$")) return "Accent must be a six-digit hex color.";
        if (BackgroundPath is null || BackgroundPath.Length > 1024 || TrackedMemberId is null || TrackedMemberId.Length > 100) return "Invalid background path or member ID.";
        return GameTheme.Validate(GameThemes) ?? WidgetCatalog.Validate(Widgets);
    }
}

public sealed record Metric(string Id, string Label, double? Value, string Unit);
public sealed record SensorReading(string Id, string Name, string HardwareId, string HardwareName,
    string HardwareType, string SensorType, double? Value, double? Minimum, double? Maximum, string Unit);
public sealed record HardwareSnapshot(IReadOnlyList<Metric> Metrics, string Status, string? Detail = null)
{
    public IReadOnlyList<SensorReading> Sensors { get; init; } = [];
    public bool IsAdministrator { get; init; }
    public bool PawnIoInstalled { get; init; }
}
public sealed record MediaSnapshot(bool Playing, string Title, string Artist, string App, double PositionSeconds, double DurationSeconds, string Status)
{
    public string? ArtworkId { get; init; }
}
public sealed record ForegroundSnapshot(int ProcessId, string ProcessName, string DisplayName, bool IsGame, string? IconId, string IconStatus)
{
    public static ForegroundSnapshot Empty { get; } = new(0, "", "Non disponibile", false, null, "unavailable");
}
public sealed record VoiceMember(string Id, string Name, bool Mute, bool Deaf);
public sealed record DiscordSnapshot(IReadOnlyList<VoiceMember> Members, VoiceMember? Tracked, string Status, string? Detail = null);
public sealed record DisplaySnapshot(bool Connected, string Port, string? DeviceId, string Status, string? Error = null)
{
    public int RecoveryAttempts { get; init; }
    public int Recoveries { get; init; }
    public long AcknowledgedFrames { get; init; }
    public string? LastTransportError { get; init; }
    public DateTimeOffset? LastAcknowledgedAt { get; init; }
    public bool UserDisconnected { get; init; }
    public string? LastFrameKind { get; init; }
    public uint? LastFrameCounter { get; init; }
}
public sealed record DeckState(DateTimeOffset Timestamp, string Profile, string ForegroundApp, HardwareSnapshot Hardware,
    MediaSnapshot Media, DiscordSnapshot Discord, DisplaySnapshot Display, string FpsStatus = "not-configured")
{
    public ForegroundSnapshot Foreground { get; init; } = ForegroundSnapshot.Empty;
    public ForegroundSnapshot? Game { get; init; }
    public WeatherSnapshot Weather { get; init; } = new();
    public NewsSnapshot News { get; init; } = new();
}
