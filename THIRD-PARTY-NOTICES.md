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
Attribution appears in the panel and configurator. Weather icons are drawn by
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
