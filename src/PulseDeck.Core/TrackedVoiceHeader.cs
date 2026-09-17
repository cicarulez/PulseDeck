namespace PulseDeck.Core;

public sealed record TrackedVoiceHeader(string Name, string Status, bool? Muted, bool Deaf)
{
    public static TrackedVoiceHeader Resolve(DiscordSnapshot discord, string memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId)) return new("DISCORD", "UTENTE NON SCELTO", null, false);
        if (discord.Status != "connected") return new("DISCORD", "NON DISPONIBILE", null, false);
        // Match the current roster, never another member or a stale Tracked snapshot.
        var member = discord.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return new("DISCORD", "UTENTE FUORI CANALE", null, false);
        return new(member.Name, member.Deaf ? "DEAF" : member.Mute ? "MUTE" : "MIC ATTIVO", member.Mute, member.Deaf);
    }
}
