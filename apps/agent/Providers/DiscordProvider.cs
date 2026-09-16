using System.Text.Json;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class DiscordProvider(HttpClient client, EmbeddedDiscordService embedded)
{
    public async Task<DiscordSnapshot> ReadAsync(DeckConfig config, CancellationToken cancellationToken)
    {
        if (config.DiscordMode == "embedded") return embedded.Read(config);
        try
        {
            using var response = await client.GetAsync(new Uri(config.DiscordBaseUrl.TrimEnd('/') + "/snapshot"), cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return new([], null, "error", "Invalid backend snapshot.");
            var data = root.GetProperty("data");
            static bool Flag(JsonElement m, string name) => m.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
            var members = data.GetProperty("members").EnumerateArray().Select(m => new VoiceMember(
                m.GetProperty("id").GetString() ?? "", m.GetProperty("name").GetString() ?? "Unknown",
                Flag(m, "mute") || Flag(m, "muted"), Flag(m, "deaf"))).ToArray();
            var tracked = members.FirstOrDefault(m => m.Id == config.TrackedMemberId);
            return new(members, tracked, "connected", "Backend reachable. Voice login readiness and speaking activity are not exposed by this backend.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new([], null, "offline", "Start discord-overlay's backend and configure its voice channel."); }
    }
}
