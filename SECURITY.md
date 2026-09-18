# Security reports

PulseDeck is early-stage software. Security fixes target the latest source and release; older versions do not have a separate maintenance branch.

Please do not post exploit details, credentials or private logs in a public issue. Use [GitHub private vulnerability reporting](https://github.com/cicarulez/PulseDeck/security/advisories/new) **if enabled**. If the form is unavailable, use a private contact method listed on [the maintainer's profile](https://github.com/cicarulez). If neither is available, open an issue asking only for a private reporting channel, without vulnerability details.

A useful report describes affected versions, prerequisites, impact and a minimal reproduction. Remove tokens, account IDs and unrelated personal information. There is no promised response SLA or bug bounty.

## Security boundaries

The agent listens on `127.0.0.1:5178`. It is designed for a trusted local user, not as a remotely exposed or multi-user service. Origin checks and a local client header do not replace authentication for a network deployment. Do not bind or proxy the API onto a public interface.

Discord credentials and the optional SteamGridDB key are encrypted with Windows DPAPI CurrentUser and are never returned by the local configuration API. Local state and logs can still reveal application names, media metadata, paths or Discord participants. See [privacy and data](docs/privacy.md).

Administrator rights are used for supported hardware sensors and ETW collection. Changes touching process launch, paths, local API access, dependency downloads or serial writes deserve particular review.
