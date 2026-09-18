# Privacy and local data

PulseDeck has no hosted account service or analytics backend. The agent runs on your Windows PC and binds its API to loopback. Optional integrations make outbound requests as described below.

## What stays on the PC

Runtime state belongs under **`%LOCALAPPDATA%\PulseDeck`**, or the directory explicitly selected with `PULSEDECK_DATA_DIR`. It is separate from the source tree and published application folder.

| Data | Storage and purpose |
| --- | --- |
| Configuration | `config.json`: widget bindings, preferences, selected paths and integration settings |
| Discord bot credentials | `discord.credentials`: encrypted with DPAPI CurrentUser |
| SteamGridDB API key | `steamgriddb.credentials`: encrypted with DPAPI CurrentUser |
| Discovered games | `game-library.json`: local paths, names and discovery metadata |
| Game artwork | `game-artwork`: downloaded covers/backgrounds, with a 30-day cache policy |
| Lyrics | `lyrics`: matched LRCLIB responses, with a 30-day cache policy |
| Logs | Local diagnostics; may contain paths, media/game identifiers or Discord diagnostic details |
| Live telemetry | Latest hardware/media/voice snapshots and short in-memory state; no continuous historical telemetry database |

A cache TTL controls reuse, not a guaranteed deletion schedule. Existing cache files can remain on disk until replaced or manually removed. The historical telemetry database discussed in the roadmap is not implemented.

DPAPI protects credentials for the Windows user; it is not a reason to share credential files or full runtime backups. The local configuration API never returns the Discord token or SteamGridDB key. Export only the diagnostics needed for a report, and inspect them first.

## What optional services receive

| Integration | Data sent / external interaction |
| --- | --- |
| Open-Meteo / location search | Typed place query or selected location coordinates |
| RSS/Atom | Requests to the feeds you enable; no full article scraping |
| Steam artwork lookup | Game-title searches and image requests |
| SteamGridDB | Game-title searches authenticated with your optional API key; image requests to its CDN |
| LRCLIB | Song title, artist, duration and album when available; no audio or Spotify credentials |
| Discord | Bot authentication, channel roster/screen-sharing flags and optional voice connection for speaking events while the tracked user is present (or any human if no user is configured), across profiles. Received audio is discarded, never recorded or played. |
| Chrome extension | YouTube main-player metadata is posted locally. Optional `tabs`/`favicon` permissions add the focused tab title and a bounded PNG from Chrome’s local favicon endpoint; no page URL is sent to the agent and no arbitrary icon URL is fetched. See the [extension guide](../apps/youtube-extension/README.md). |

These providers see normal network request information, including your IP address. Their services and policies are independent of PulseDeck. Disable an integration if you do not want its requests.

Chrome, Windows Terminal and Explorer folder-window titles are read natively for the active-app widget, even without the extension. Explorer full-path titles are reduced to the folder label. Packaged Windows apps use their locally registered display names and logos; PulseDeck does not query the Store or persist these icons. This can include private/incognito window titles. Optional Chrome favicons use only the focused non-incognito HTTP/HTTPS tab and expire after six seconds; metadata and icon caches stay in memory.

## Before sharing screenshots or logs

Remove bot tokens, API keys, encrypted credential files, account/server/channel IDs, private names, home-directory paths, browser URLs and unrelated diagnostic content. Do not publish runtime backups. Use the [documentation gallery fixtures](../tools/docs-gallery/README.md) for public screenshots when real data is unnecessary.
