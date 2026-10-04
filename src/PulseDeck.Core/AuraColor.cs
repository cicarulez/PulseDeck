using System.Text.Json;

namespace PulseDeck.Core;

public sealed record AuraSnapshot(string Status = "disabled", string? Color = null,
    DateTimeOffset? ReceivedAt = null, long Samples = 0, string? Detail = null, string[]? Colors = null);

// Versioned, passive HAL diagnostics. Monotonic heartbeat rejects stale files from
// stopped hosts and previous boots without expiring a legitimate static color.
public sealed record AuraReport
{
    public int ProtocolVersion { get; init; }
    public string State { get; init; } = "";
    public DateTimeOffset Utc { get; init; }
    public int Pid { get; init; }
    public ulong HeartbeatTick { get; init; }
    public long RawSamples { get; init; }
    public uint RawWord { get; init; }
    public ulong RawSampleTick { get; init; }
    public int LastEffectMethod { get; init; }
    public uint LastEffectId { get; init; }
    public uint LastEffectCount { get; init; }
    public uint LastEffectVariant { get; init; }
    public int LedCount { get; init; }
    public int RawCount { get; init; }
    public uint[]? RawWords { get; init; }
}

public static class AuraColor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static AuraReport? Parse(string json) => JsonSerializer.Deserialize<AuraReport>(json, Json);

    public static AuraSnapshot Evaluate(AuraReport report, DateTimeOffset now, ulong tick, bool hostAlive)
    {
        if (report.ProtocolVersion is not (1 or 2)) return new("incompatible", Detail: "Aggiorna il ricevitore Aura di PulseDeck.");
        if (!hostAlive || report.State != "running" || report.Pid <= 0
            || report.HeartbeatTick > tick || tick - report.HeartbeatTick > 3000
            || report.Utc > now.AddSeconds(2) || report.Utc < now.AddSeconds(-5))
            return new("unavailable", Detail: "Il ricevitore Aura non è attivo o non risponde.");
        if (report.RawSamples <= 0 || report.RawSampleTick == 0 || report.RawSampleTick > report.HeartbeatTick)
            return new("waiting", Detail: "Seleziona Dispositivi esterni in Aura Sync e applica un effetto.");
        var count = report.ProtocolVersion == 1 ? 1 : report.LedCount;
        if (report.LastEffectMethod != 3 || report.LastEffectId != 0
            || count is < 1 or > 64 || report.LastEffectCount != count || report.LastEffectVariant != 8211)
            return new("unsupported", Detail: "L’effetto Aura ricevuto non è supportato.");
        if (report.ProtocolVersion == 2 && (report.RawCount != count || report.RawWords?.Length != count
            || report.RawWords[0] != report.RawWord))
            return new("unsupported", Detail: "Il gruppo di colori Aura ricevuto è incompleto.");
        var age = report.HeartbeatTick - report.RawSampleTick;
        if (age > (ulong)Math.Max(0, (report.Utc - DateTimeOffset.UnixEpoch).TotalMilliseconds))
            return new("unavailable", Detail: "Timestamp Aura non valido.");
        var colors = report.ProtocolVersion == 1 ? [Hex(report.RawWord)] : report.RawWords!.Select(Hex).ToArray();
        return new("connected", colors[0], report.Utc.AddMilliseconds(-(double)age), report.RawSamples, Colors: colors);
    }

    // Observed on the installed ASUS service: numeric 0xAABBGGRR. Ignore high byte.
    public static string Hex(uint word) => $"#{word & 255:X2}{word >> 8 & 255:X2}{word >> 16 & 255:X2}";
    public static string Accent(DeckConfig config, AuraSnapshot aura) =>
        config.AuraEnabled && aura.Status == "connected" && aura.Color is { } color ? color : config.AccentColor;
}
