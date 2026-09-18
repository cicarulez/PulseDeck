namespace PulseDeck.Core;

public static class DiscordVoicePolicy
{
    public static ulong SelectedChannel(DeckConfig config, ulong importedChannel) =>
        ulong.TryParse(config.DiscordVoiceChannelId, out var selected) && selected > 0 ? selected : importedChannel;
    // The legacy setting name is retained so existing configurations keep their choice.
    // Without a tracked user, follow occupancy by human participants in the configured channel.
    public static bool ShouldConnect(DeckConfig config, IEnumerable<string> humanMemberIds) =>
        config.DiscordMode == "embedded" && config.GamingVoiceActivity
        && humanMemberIds.Any(id => string.IsNullOrWhiteSpace(config.TrackedMemberId) || id == config.TrackedMemberId);
}
