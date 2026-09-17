# Third-party components

PulseDeck is licensed under GPL-3.0-or-later. It includes an adaptation of the
TURZX revision C frame encoding from **turing-smart-screen-python**, commit
`2b33ab4f00a096916dd6a1174441a53a7ec33b03`:

- Source: https://github.com/mathoudebine/turing-smart-screen-python
- Original file: `library/lcd/lcd_comm_rev_c.py`
- PulseDeck adaptation: `src/PulseDeck.Core/TurzxProtocol.cs`
- Copyright (C) 2021 Matthieu Houdebine
- Copyright (C) 2023 Alex W. Baulé
- Copyright (C) 2023 Arthur Ferrai
- License: GPL-3.0-or-later; see `LICENSE`.

The Python diagnostic in `tools/turzx-probe` was developed in the local
Discord Overlay project and imports a separately installed upstream driver.

Other dependencies retain their own licenses:

| Component | License | Project |
| --- | --- | --- |
| .NET / ASP.NET Core / SignalR / System.Drawing.Common | MIT | https://github.com/dotnet |
| Electron | MIT; bundled Chromium/Node retain their own notices | https://github.com/electron/electron |
| Angular | MIT | https://github.com/angular/angular |
| RxJS | Apache-2.0 | https://github.com/ReactiveX/rxjs |
| SkiaSharp | MIT; bundled Skia has its own notices | https://github.com/mono/SkiaSharp |
| LibreHardwareMonitor | MPL-2.0 and component-specific notices | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| Discord.Net | MIT | https://github.com/discord-net/Discord.Net |
| xUnit.net | Apache-2.0 | https://github.com/xunit/xunit |

Dependency license files included by their packages must accompany redistribution.
No TURZX binaries, original theme files, EA artwork or game assets are included.
Background images are selected from the user's local files.

The optional weather layout uses data from [Open-Meteo](https://open-meteo.com/)
and location search based on [GeoNames](https://www.geonames.org/). See the service's
[terms](https://open-meteo.com/en/terms), [forecast documentation](https://open-meteo.com/en/docs)
and [geocoding attribution](https://open-meteo.com/en/docs/geocoding-api).
Attribution appears in the configurator. Weather icons are drawn by
PulseDeck; no third-party icon collection is bundled.

## News feed presets

Preset URLs are taken from the publishers' own feed catalogs:
[ANSA RSS](https://www.ansa.it/sito/static/ansa_rss.html) and
[Multiplayer.it](https://multiplayer.it/feed/). Headlines remain the property of their
publishers; PulseDeck is a personal feed reader displaying attribution, publication
time and links to the original articles. Feed content is fetched only after opt-in,
kept in memory and is not bundled in the application or repository. No article text
or publisher images are included in the distribution. Availability and publisher
terms may change; users can disable or replace any preset.

## Gaming telemetry and voice

Windows builds package PresentMon 2.5.1 (MIT), libdave 1.2.0 (MIT, including
BoringSSL/MLS++/nlohmann notices), and Discord.Net's Windows Opus and libsodium
binaries (BSD/ISC). `scripts/gaming-dependencies.json` pins the upstream downloads
and SHA-256 digests. Their license files accompany the binaries under `licenses/`.
No PresentMon service or overlay is installed: PulseDeck runs the console collector
only for the selected game process.

Game covers can be fetched from Steam Store by exact title match. Artwork remains
owned by its publisher, cached under the user's PulseDeck data directory, and is
not redistributed with the application. Store endpoints and NVIDIA's local catalog
are optional, undocumented formats; missing or ambiguous matches remain unavailable.

PulseDeck 0.3.1 builds Discord.Net.WebSocket 3.20.1 from the pinned upstream source
with a DAVE recognized-roster fix. It retains MIT licensing and the original
assembly identity; informational version identifies `pulsedeck.dave-roster.1`.
See `src/PulseDeck.Discord/README.md` and `PatchDiscordRoster.cs` for exact provenance
and the source change. Core/Rest/Dave remain the unmodified NuGet dependencies.
