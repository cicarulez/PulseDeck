# Roadmap

PulseDeck is an early-stage project built around one verified Windows/TURZX setup. The next goal is to make it easier for other people to run, understand and test. These are directions for contribution, not promised release dates.

Updated 2026-10-04 · source snapshot 0.10.0. See [development notes](releases/v0.10.0.md).

## Available now

- Passive Aura color following across 16 zones, multicolor wordmark/lyrics/spectrum and manual fallback. Manual display brightness and Aura OFF restoration, including Aura unavailability.
- Independent animation renderer and USB writer, with browser preview recovery after delayed images.
- Shared notifications with Gmail OAuth, unread-message counts and Desktop-only arrival animation; see [setup and remaining validation](notifications.md). New calendar additions also notify through the existing iCal connection; time-based reminders remain future work.

- Google Calendar appointments in the empty Desktop media panel; Discord server labels and configurable channel/user selection.

- Configurable sensor values, bars and rings; combined network widget and power/temperature trends.
- Desktop/weather, Gaming and Spotify lyrics compositions, including Music's nine-widget grid and optional three-widget Spotify audio spectrum.
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
2. **Windows and USB reliability.** More physical unplug/replug, sleep/wake and long-running sessions. Keep verified full frames during animations; sustained high-rate partial and grouped-region trials still produce rejections/timeouts. Compare firmware ACK timing and transport diagnostics before raising production cadence. CI cannot replace these checks.
3. **Layout flexibility and accessibility.** Per-profile composition controls, localization and readable text at physical size.
4. **Broader game/media validation.** Library formats, ambiguous executables, FPS collector readiness, voice reconnects and source arbitration.

## Larger proposals

| Proposal | Scope and constraints |
| --- | --- |
| Historical telemetry | Optional local recording, likely SQLite with retention/aggregation. Consider storage, writes and privacy before recording everything. Not implemented. |
| Multiple displays | Independent configuration and transport per screen, shared provider acquisition. A second 3.5″ panel is not currently supported. |
| Aura color following | Optional passive receiver, configurator toggle and panel rendering implemented. Static transitions and Color cycle physically verified on 2026-10-04; manual fallback retained. Experimental 16-zone Rainbow reception verified; wordmark/lyrics/music spectrum and solid sensor accents implemented. Next: full Windows reboot persistence and broader effect/device compatibility. No RGB control acquisition. |
| Music layout evolution | Dedicated Spotify composition, optional three-widget/process-isolated audio spectrum and read-only [account extras](spotify.md) are implemented. Base Music remains login-free. Future work can improve lyrics coverage/fallbacks and layout customization; playback controls are deferred. |
| Refresh optimization | The [recorded refresh experiments](validation.md) guide further benchmarking. Production animation targets about 4 fps with verified full frames; high-rate partial/multi-region trials remain experimental after sustained rejections/timeouts. The independent full-frame probe reached 4.33 fps over five minutes with no errors or reported artifacts; raising Windows process priority brought no meaningful gain. Next: firmware processing/ACK timing and a measured cost model. Browser preview runs independently near 10 fps. |
| Internal video playback | [Research notes](turzx_internal_playback_research.md) examine prerecorded video beneath live graphics after the user observed smooth vendor AMD playback. Upload/playback commands, alpha composition, erasure and physical stability still need verification on the tested ROM. Not implemented; does not replace live Aura, lyrics or spectrum data. |
| Daily lifecycle | Tray controls and a clearer update/recovery experience. |

For protocol and runtime constraints see [architecture](architecture.md). For what was actually tested, see [validation](validation.md). The detailed earlier investigation is retained in [development history](development-history.it.md); historical entries may describe features that have since shipped.

### Gmail connection after the development phase

The current personal-project OAuth setup is provisional. Evaluate a PulseDeck-managed
desktop OAuth client only if users can connect through Google consent without their
own Cloud project, JSON import or website. Assess verification and maintenance first;
keep API access and encrypted user tokens local. No migration is committed or scheduled.
If the alternative still requires the same per-user setup, retain the current solution.
