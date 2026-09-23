namespace PulseDeck.Core;

public sealed record GameTheme(string ProcessName, string BackgroundPath)
{
    // Null preserves the old behavior where one manual image served both the widget and background.
    public string? CoverPath { get; init; }

    public static string? Validate(GameTheme[] themes)
    {
        if (themes is null || themes.Length > 50) return "At most 50 game backgrounds are allowed.";
        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var theme in themes)
        {
            if (theme is null || string.IsNullOrWhiteSpace(theme.ProcessName) || theme.ProcessName.Length > 100
                || theme.ProcessName.IndexOfAny(['/', '\\', ':']) >= 0
                || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(theme.ProcessName))
                || theme.BackgroundPath is null || theme.BackgroundPath.Length > 1024
                || theme.CoverPath is { Length: > 1024 })
                return "Invalid game background or process name.";
            if (!processes.Add(Path.GetFileNameWithoutExtension(theme.ProcessName))) return "Duplicate game background process.";
        }
        return null;
    }

    public static string BackgroundFor(DeckState state, DeckConfig config)
    {
        if (state.Profile != "gaming" || state.Game is not { } game || !ProfileSelector.IsGame(config, game.ProcessName))
            return config.BackgroundPath;
        var manual = ManualBackgroundFor(state, config);
        return manual.Length > 0 ? manual : state.GameArtwork.BackgroundPath ?? config.BackgroundPath;
    }

    public static string ManualBackgroundFor(DeckState state, DeckConfig config)
    {
        if (state.Profile != "gaming" || state.Game is not { } game || !ProfileSelector.IsGame(config, game.ProcessName)) return "";
        return Find(state, config)?.BackgroundPath is { Length: > 0 } path ? path : "";
    }

    public static string ManualCoverFor(DeckState state, DeckConfig config)
    {
        if (state.Profile != "gaming" || state.Game is not { } game || !ProfileSelector.IsGame(config, game.ProcessName)) return "";
        var theme = Find(state, config);
        // CoverPath is null only for configurations saved before separate image choices existed.
        return theme?.CoverPath ?? theme?.BackgroundPath ?? "";
    }

    private static GameTheme? Find(DeckState state, DeckConfig config) => config.GameThemes.FirstOrDefault(t =>
        string.Equals(Path.GetFileNameWithoutExtension(t.ProcessName),
            Path.GetFileNameWithoutExtension(state.Game!.ProcessName), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Keep artwork during a brief automatic Alt-Tab without claiming the game is still in the foreground.</summary>
public sealed class GameSceneSelector
{
    private ForegroundSnapshot? game;
    private DateTimeOffset? awaySince;

    public ForegroundSnapshot? Select(DeckConfig config, string profile, ForegroundSnapshot foreground, DateTimeOffset now)
    {
        if (profile != "gaming") { game = null; awaySince = null; return null; }
        if (foreground.IsGame && ProfileSelector.IsGame(config, foreground.ProcessName))
        {
            awaySince = null;
            return game = foreground;
        }
        awaySince ??= now;
        if (config.ProfileMode != "auto" || now - awaySince.Value >= TimeSpan.FromSeconds(config.ProfileDelaySeconds)
            || game is not null && !ProfileSelector.IsGame(config, game.ProcessName)) game = null;
        return game;
    }
}
