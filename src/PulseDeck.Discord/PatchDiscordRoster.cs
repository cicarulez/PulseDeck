// MSBuild task fragment. Upstream source remains under ignored obj/, pinned by SHA-256.
var source = System.IO.File.ReadAllText(SourceFile);
const string marker = "// PulseDeck: use the authenticated channel roster, including silent members and self.";
if (!source.Contains(marker))
{
    const string anchor = "    private async ValueTask OnMLSWelcomeAsync(";
    if (!source.Contains(anchor) || !source.Contains("_session.ProcessWelcome(payload, _decryptors.Keys)"))
        throw new System.InvalidOperationException("Unexpected Discord.Net DAVE source; review the patch before building.");
    source = source.Replace("using System;", "using System;\nusing System.Linq;");
    // The decryptor map is populated lazily, so it is incomplete when a Welcome arrives.
    source = source.Replace("_decryptors.Keys", "RecognizedUserIds()");
    var helper = marker + @"
    private ICollection<ulong> RecognizedUserIds() =>
        (_client.Guild.GetVoiceChannel(_client.ChannelId)?.ConnectedUsers.Select(user => user.Id)
            ?? Enumerable.Empty<ulong>())
        .Concat(_decryptors.Keys).Append(SelfUserId).Distinct().ToArray();

";
    source = source.Replace(anchor, "    " + helper + anchor);
    System.IO.File.WriteAllText(SourceFile, source);
}
