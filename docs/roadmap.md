# Roadmap

PulseDeck is an early-stage project built around one verified Windows/TURZX setup. The next goal is to make it easier for other people to run, understand and test. These are directions for contribution, not promised release dates.

## Available now

- Google Calendar appointments in the empty Desktop media panel; Discord server labels and configurable channel/user selection.

- Configurable sensor values, bars and rings; combined network widget and power/temperature trends.
- Desktop/weather, Gaming and Spotify lyrics compositions, including Music's nine-widget grid.
- Foreground-based automatic profiles, persistent game session timer and PresentMon application FPS.
- Steam/EA local game discovery, manual exceptions and optional SteamGridDB artwork.
- Windows media integration and an optional YouTube main-player extension.
- Discord roster, mute/deaf and Gaming speaking activity.
- Local configuration, DPAPI credentials, shared preview/display rendering and bounded USB recovery.

## Good starting points

| Area | A useful first contribution | How to validate |
| --- | --- | --- |
| Documentation | Walk through setup on a second PC and fix unclear steps | Record actual steps and missing prerequisites |
| Localization | Propose an English/Italian message strategy for configurator and panel | Keep user-visible strings consistent; review small-screen fit |
| Hardware reports | Document a supported sensor/board combination | Sanitized identifiers, permissions and unavailable readings |
| Media | Reproduce an edge case with pause, track change or multiple players | Describe exact transitions; test without stale metadata |
| Layouts | Improve truncation, empty states or readability | Production-renderer fixture plus visual review |
| Tests | Cover a reported parsing or state-transition regression | Small input that fails before the fix |

## Next priorities

1. **Onboarding and distribution.** Reliable first-run guidance, simpler Discord credential setup, reproducible packages and usable release notes.
2. **Windows and USB reliability.** More physical unplug/replug, sleep/wake and long-running sessions. Investigate why partial-frame rejections became more frequent with recent Music use; 0.7.5 keeps full updates after rejection as a mitigation, with transfer diagnostics for comparison. CI cannot replace these checks.
3. **Layout flexibility and accessibility.** Per-profile composition controls, localization and readable text at physical size.
4. **Broader game/media validation.** Library formats, ambiguous executables, FPS collector readiness, voice reconnects and source arbitration.

## Larger proposals

| Proposal | Scope and constraints |
| --- | --- |
| Historical telemetry | Optional local recording, likely SQLite with retention/aggregation. Consider storage, writes and privacy before recording everything. Not implemented. |
| Multiple displays | Independent configuration and transport per screen, shared provider acquisition. A second 3.5″ panel is not currently supported. |
| Aura color following | Read the PC's existing lighting state without acquiring RGB control. Research is paused; a controller replacement does not meet the requirement. |
| Music layout evolution | Dedicated Spotify composition is implemented. Future work can improve lyrics coverage/fallbacks and layout customization. |
| Daily lifecycle | Tray controls and a clearer update/recovery experience. |

For protocol and runtime constraints see [architecture](architecture.md). For what was actually tested, see [validation](validation.md). The detailed earlier investigation is retained in [development history](development-history.it.md); historical entries may describe features that have since shipped.
