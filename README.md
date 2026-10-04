<div align="center">
  <img src="docs/images/hero.png" alt="PulseDeck — your PC, at a glance. Desktop, Gaming and Music previews rendered by PulseDeck with sample data." width="100%">
  <p><strong>A Windows companion for your dedicated PC display.</strong><br>Hardware telemetry, game sessions, Discord and Spotify — where you can see them.</p>
  <p>
    <a href="LICENSE"><img alt="License: GPL-3.0-or-later" src="https://img.shields.io/badge/license-GPL--3.0--or--later-a9ff69?labelColor=111c22"></a>
    <img alt="Platform: Windows x64" src="https://img.shields.io/badge/platform-Windows_x64-9bc8f5?labelColor=111c22">
    <img alt="Status: early development" src="https://img.shields.io/badge/status-early_development-f5cb8b?labelColor=111c22">
    <a href="https://buymeacoffee.com/cicarulez"><img alt="Support PulseDeck on Buy Me a Coffee" src="https://img.shields.io/badge/Support-Buy_Me_a_Coffee-a9ff69?logo=buymeacoffee&amp;logoColor=a9ff69&amp;labelColor=111c22"></a>
  </p>
  <p><a href="docs/getting-started.md">Get started</a> · <a href="docs/gallery.md">Gallery</a> · <a href="CONTRIBUTING.md">Contribute</a> · <a href="docs/roadmap.md">Roadmap</a> · <a href="docs/README.it.md">Italiano</a></p>
</div>

**Development snapshot: v0.10.0 · 4 October 2026.** See the [development notes](docs/releases/v0.10.0.md); this does not imply a published release. The [project website](https://pulsedeck.davidecappa.it/) presents the documentation as web pages.

## What is PulseDeck?

PulseDeck turns a **TURZX 8.8″ 1920 × 480 USB panel** into a contextual dashboard for your Windows PC. It runs locally, gathers the information you choose, and switches between Desktop, Gaming and Music as you work, play and listen.

The browser configurator and physical panel share the **same renderer**. What you arrange in the preview is what the agent sends to the display. No PulseDeck account or hosted backend is required; optional weather, artwork, lyrics and Discord features contact their respective services.

**Early development, tested on one display revision.** The verified device is `chs_88inch.dev1_rom1.90`, USB `0525:A4A7`. Other sizes and revisions are not yet supported. The configurator and display labels are currently in Italian; localization is a welcome contribution.

## Integrations at a glance

<p>
  <a href="tools/aura-probe/README.md"><img alt="ASUS Aura Sync: experimental RGB following" src="https://img.shields.io/badge/ASUS_Aura_Sync-RGB_following_%C2%B7_experimental-a9ff69?labelColor=111c22"></a>
  <a href="docs/spotify.md"><img alt="Spotify: now playing" src="https://img.shields.io/badge/Spotify-now_playing-a9ff69?labelColor=111c22"></a>
  <a href="docs/getting-started.md#discord-credentials"><img alt="Discord: voice activity" src="https://img.shields.io/badge/Discord-voice_activity-a9ff69?labelColor=111c22"></a>
</p>
<p>
  <a href="docs/getting-started.md"><img alt="Steam and EA: library discovery" src="https://img.shields.io/badge/Steam_%2B_EA-library_discovery-9bc8f5?labelColor=111c22"></a>
  <a href="docs/calendar.md"><img alt="Google Calendar: appointments" src="https://img.shields.io/badge/Google_Calendar-appointments-9bc8f5?labelColor=111c22"></a>
  <a href="docs/getting-started.md"><img alt="SteamGridDB: game artwork" src="https://img.shields.io/badge/SteamGridDB-game_artwork-9bc8f5?labelColor=111c22"></a>
</p>

These labels describe specific PulseDeck features; some require optional setup. Aura following uses a separately installed experimental receiver, tested with static colors and Color cycle on the development PC. An experimental 16-zone build also receives simultaneous Rainbow colors and uses them on the wordmark, active lyrics, music progress bar and audio spectrum; sensor bars retain a solid color each.

*Independent community integrations. No vendor sponsorship, endorsement or certification is claimed. Names belong to their respective owners; these are text-only feature badges, not official vendor logos.*

## Where it started

The idea began with [Discord Overlay](https://github.com/cicarulez/discord-overlay): keeping Discord voice participants visible while gaming. Overlay restrictions encountered in games, including anti-cheat limitations, inspired a move to a dedicated external display. PulseDeck grew from that need, adding hardware telemetry, game sessions, artwork and music to the original voice-information concept.

## Three ways to use the same screen

### Desktop · Keep the essentials in view

![Desktop layout: weather, twelve hardware widgets, media and Discord](docs/images/desktop.png)

CPU, GPU and RAM rings. Temperatures, power, clocks, fans and combined download/upload. Add weather and RSS headlines, or use the compact 16-widget layout. Unavailable readings stay visibly unavailable.

### Gaming · Follow the session

![Gaming layout: voice participants, hardware readings, game artwork, session timer and FPS](docs/images/gaming.png)

Discover games in Steam and EA libraries, show artwork, keep the session timer through Alt-Tab, and display application FPS from PresentMon. Discord speaking activity highlights the current speaker, and the voice connection stays active through Alt-Tab while you remain in the configured channel.

### Music · Give Spotify its own space

![Music layout: original sample cover, synchronized sample lyrics and nine hardware widgets](docs/images/music.png)

![Optional Music layout with three hardware widgets and synthetic multicolor spectrum](docs/images/music-spectrum.png)

Choose nine hardware widgets or the first three above a real 24-band spectrum of the Spotify app. The spectrum uses Windows process loopback, excludes other applications and analyses audio locally in memory.

Spotify gets cover art, track progress and optional synchronized lyrics from LRCLIB, alongside the selected widget layout. Track changes keep the layout stable while lyrics load. Automatic priority is **Gaming → Music → Desktop**; manual selection takes precedence.

An [optional Spotify account connection](docs/spotify.md) adds all track artists, the active device and upcoming tracks. Music works without a login and falls back to local metadata when the connection is unavailable.

*These are real renderer outputs using fictional names, sample readings and original artwork/lyrics. They are documentation previews, not hardware benchmarks or photographs. See [image provenance](docs/gallery.md).*

## Built for everyday use

| Capability | Available today |
| --- | --- |
| Hardware | LibreHardwareMonitor sensors, configurable values/bars/rings, temperature and power trends |
| Games | Steam/EA discovery, manual exceptions, per-game artwork, persistent session timer, application FPS |
| Calendar | [Google Calendar](docs/calendar.md) appointments and countdown in the empty Desktop media area |
| Voice | Discord server/channel labels, configurable voice channel and tracked user, mute/deaf, speaking highlights and screen-sharing indicators; expanded Desktop roster when media is absent |
| Media | Windows media sessions; optional Chrome extension prioritizes the main YouTube player |
| Active app | Chrome/Terminal tab titles, Explorer folder names, registered Windows app names/logos and optional Chrome favicons |
| Spotify | Dedicated lyrics layout; other media sources keep the regular media panel |
| Aura Sync | Optional passive 16-zone receiver; multicolor accents, lyrics and spectrum; manual fallback and readable text |
| Artwork | Optional SteamGridDB key, Steam fallback, local cache and manual backgrounds |
| Daily controls | Volume, clock, foreground app or Terminal tab title, manual/automatic profiles |
| Display | Verified device identification, manual brightness, Aura OFF dimming with brightness restoration, bounded recovery and explicit disconnect |

Multiple displays, freely positioned layouts and historical telemetry are on the [roadmap](docs/roadmap.md). The renderer targets one update per second for ordinary telemetry and up to ten for animations, independently of USB delivery, which targets four updates per second. The browser preview recovers from slow image loads without requiring a refresh. Actual cadence depends on rendering and USB transfer; see the [measured results](docs/validation.md). This is not a video display engine.

## Try it

1. Use a Windows x64 machine. Build the complete package using the [source instructions](CONTRIBUTING.md#build-the-windows-package), or use a Windows archive **if one is available** on [Releases](https://github.com/cicarulez/PulseDeck/releases).
2. Extract the entire package and run `Start-PulseDeck.cmd`. The configurator opens at **http://127.0.0.1:5178**.
3. Choose your sensors and preview the layout. A physical display is not required for the configurator or preview.
4. For the supported panel, close the vendor's TURZX app, choose your actual COM port and connect.

CPU, motherboard and fan readings may require **PawnIO and administrator rights**. The published agent includes its .NET runtime; SDKs and Node.js are only needed to build it. Follow the [setup guide](docs/getting-started.md) for drivers, Discord, optional integrations and automatic startup.

## Help build it

You do not need this display to contribute. The profile rules, protocol and rendering tests run on Linux; the Angular configurator can be built independently. Actual media, sensor, voice and USB integration checks need a signed-in Windows session.

Useful places to start:

- **Design and localization:** English UI, clearer empty states and readable small-screen layouts.
- **Windows and hardware:** sensor compatibility reports, suspend/resume and USB reconnection validation.
- **Media and games:** multi-player session selection, library edge cases and artwork matching.
- **Documentation:** setup walkthroughs, troubleshooting and device reports with sensitive details removed.

Read [CONTRIBUTING.md](CONTRIBUTING.md), pick a bounded item from the [roadmap](docs/roadmap.md), or [open an issue](https://github.com/cicarulez/PulseDeck/issues). Start a discussion in an issue before a large change so we can agree on scope.

If you'd like to support development financially, you can [buy me a coffee](https://buymeacoffee.com/cicarulez). Code, testing, documentation and feedback are equally welcome.

## Under the hood

```mermaid
flowchart LR
    Windows[Windows sensors, apps and media] --> Agent[.NET agent]
    Services[Optional external services] --> Agent
    Agent --> State[Shared state and profile selection]
    State --> Renderer[SkiaSharp renderer]
    Renderer --> Preview[Angular preview]
    Renderer --> USB[Verified USB transport]
    USB --> Panel[TURZX panel]
    Preview -->|Local configuration| Agent
```

| Area | Technology |
| --- | --- |
| Agent and providers | .NET 10, ASP.NET Core, SignalR |
| Configurator | Angular 22, TypeScript, standalone components |
| Rendering | SkiaSharp, shared by preview and panel |
| Portable logic | C# contracts, profile rules, protocol and regression tests |
| Optional desktop window | Electron wrapper around the local configurator |

See [architecture](docs/architecture.md), [privacy and data](docs/privacy.md), [hardware compatibility](docs/hardware.md) and the [validation record](docs/validation.md).

## License and acknowledgements

**GPL-3.0-or-later.** See [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md). The TURZX frame encoder includes an adaptation from [turing-smart-screen-python](https://github.com/mathoudebine/turing-smart-screen-python).

Built with LibreHardwareMonitor, SkiaSharp, Discord.Net, PresentMon, Angular and .NET. PulseDeck is an independent community project, not affiliated with TURZX, Discord, Spotify, Valve, EA or ASUS. Game artwork, album covers and lyrics fetched during use remain with their respective rights holders and are not bundled in the repository.
