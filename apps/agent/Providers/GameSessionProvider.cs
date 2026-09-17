using System.Diagnostics;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class GameSessionProvider
{
    public GameSession? Read(ForegroundSnapshot? game, DateTimeOffset now)
    {
        if (game is null) return null;
        try
        {
            using var process = Process.GetProcessById(game.ProcessId);
            if (process.HasExited || !string.Equals(process.ProcessName, game.ProcessName, StringComparison.OrdinalIgnoreCase)) return null;
            return GameSessions.FromProcess(process.Id, process.ProcessName, process.StartTime.ToUniversalTime(), now);
        }
        catch { return null; } // A denied start time is unavailable, never a fabricated timer.
    }
}
