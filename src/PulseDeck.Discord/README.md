# Discord.Net Voice roster compatibility patch

This project builds **only** Discord.Net.WebSocket 3.20.1 from upstream commit
`f63bed073b08f25a856bed4a003849f7e600a405`. Core, Rest and Dave remain the official
3.20.1 NuGet packages. The archive is verified against SHA-256 before extraction
into ignored `obj/upstream`; no upstream binary or source archive is committed.
First build needs HTTPS access to codeload.github.com. The output keeps the assembly
identity, with informational version `3.20.1+pulsedeck.dave-roster.1`.

`PatchDiscordRoster.cs` changes the recognized-member input to DAVE MLS Welcome
and proposals. Upstream uses the lazily populated decryptor dictionary, which can
omit silent existing participants and the bot itself when a Welcome arrives.
libdave 1.2.0 rejects that Welcome, producing repeated roster/key-negotiation errors.
The fix adds IDs from the authenticated Gateway channel roster and self, retaining
the Voice Gateway's known decryptor IDs. Unknown IDs from a Welcome are **not**
accepted automatically; encryption and membership validation remain enabled.

All other WebSocket source is unmodified. The patch is reapplied from the checked
archive on each compile and fails if the expected source changes. Remove this
project and restore the official PackageReference once an upstream release includes
an equivalent fix verified against the same reconnect/roster cases.

Upstream is MIT licensed: https://github.com/discord-net/Discord.Net/blob/3.20.1/LICENSE
