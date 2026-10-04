# Architecture

PulseDeck separates acquisition, portable state rules, rendering and transport. It runs in the signed-in Windows session because foreground windows, media sessions and DPAPI are user-scoped.

## Runtime flow

```mermaid
flowchart TD
    Providers[Hardware / foreground / media / voice providers] --> Runtime[DeckRuntime]
    Remote[Optional weather / news / artwork / lyrics] --> Runtime
    Runtime --> Rules[Core profile and session rules]
    Rules --> Snapshot[DeckState]
    Snapshot --> Renderer[DeckRenderer / SkiaSharp]
    Snapshot --> Hub[Local API and SignalR]
    Renderer --> PNG[PNG preview]
    Renderer --> BGRA[BGRA frame]
    PNG --> UI[Angular configurator]
    Hub --> UI
    UI --> Config[Validated configuration / ConfigStore]
    Config --> Runtime
    BGRA --> Delivery[Acknowledged-frame delivery]
    Delivery --> Serial[Verified TURZX serial adapter]
```

## Projects and boundaries

- **`apps/agent`** owns Windows integration, provider lifetimes, the local ASP.NET Core API, rendering and serial delivery.
- **`src/PulseDeck.Core`** contains portable contracts, validation, game/library parsing, profile selection, lyrics parsing, widget resolution and frame protocol/recovery rules.
- **`apps/configurator`** edits configuration and shows state/preview. It does not reproduce the panel's rendering logic.
- **`src/PulseDeck.Discord`** builds pinned upstream WebSocket source with a documented roster fix. See its [provenance](../src/PulseDeck.Discord/README.md).
- **`apps/youtube-extension`** is an optional local YouTube main-player source with Windows media fallback, plus opt-in focused Chrome tab favicons. Native foreground titles work independently; favicon metadata must be fresh and match the native tab title.
- **`apps/desktop`** is an optional Electron wrapper around the same local configurator.

## Profiles and media identity

Automatic selection favors a recognized foreground game, then playing media, then Desktop. Candidates must survive the configured debounce. Manual selection applies immediately. Spotify's exit to Desktop has a minimum eight-second grace period to cover brief track transitions; Gaming retains its normal entry delay.

Game session identity includes PID and process start time. The timer measures the lifetime of the current process session, surviving Alt-Tab without merging reused PIDs. Automatic discovery matches executable paths from local Steam/EA metadata, with explicit manual overrides.

Windows media is the baseline source. The optional YouTube bridge identifies the main player and prefers playback to paused sessions; it expires to the Windows fallback. Spotify lyrics require a Spotify Windows media identity and an eligible track. Requests validate title, artist and duration, cache matches locally and clear stale text on track/source changes. Loading/track gaps keep the selected Music composition stable.

## Providers and unavailable data

Providers produce typed snapshots, including status and diagnostics. Sensor updates preserve healthy devices when another fails. Raw sensor ID **and name** distinguish collisions. Missing readings stay unavailable; zero is a value, not evidence of failure.

Configured LPC sensor loss can trigger bounded motherboard-only reinitialization when PawnIO and elevation are available. This does not change fan curves or acquire RGB control.

Optional Aura following uses a separate native COM destination under
`tools/aura-probe`, discovered by ASUS as “Dispositivi esterni”. LightingService
delivers colors to that destination; `AuraColorProvider` reads its versioned
SYSTEM-profile report. Fresh heartbeats and host identity distinguish a valid
static color from a stale file. `DeckState.Aura` carries availability and the
decoded color plus an ordered `Colors` array. Protocol 2 validates complete
multizone frames against the declared LED count; protocol 1 remains supported.
The renderer fits the received spectrum to the PULSEDECK wordmark, active lyrics
and music progress bar. Sensor bars/rings retain a solid color selected by their
horizontal position. Neutral/status text is independent of Aura; other accent
text follows its local zone. Animation comes from incoming frames. `AuraEnabled` defaults to false; when enabled the shared renderer
targets four updates per second while animated, keeps the manual accent as unavailable fallback,
and lightens dark accent text. Neither the agent nor receiver acquires RGB control.
Dark (OFF) retains the old RGB frame on the tested service. The provider therefore
also reads the bounded LightingService `LastProfile.xml` Group/isenabled switch,
requiring EXTERNAL_GENERAL in the synchronized group, a verified live service
and a healthy receiver. A confirmed OFF sets `Aura.Status=off`; malformed or
unavailable power state cannot turn the panel off. No ASUS settings are written.
The agent still runs as the elevated interactive user, not SYSTEM. See the
[receiver instructions](../tools/aura-probe/README.md) and dated validation evidence.

Optional `MusicSpectrum` selects the Spotify process tree in the current Windows
session and uses WASAPI process loopback (Windows build 20348 or newer). It never
falls back to the full system mix or a microphone. Ambiguous or unsupported players
are unavailable. Capture stops on pause, profile exit or disabling the option.
Stereo PCM is analysed in memory using a 2048-sample Hann FFT and 24 logarithmic
bands; only normalized levels enter snapshots. No audio file is written. Readings
older than 300 ms render as zero. See Microsoft's [process-loopback sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/).

Speaking is an explicit voice event, not the inverse of mute/deaf. Every Discord roster highlights speakers only when the voice connection is available. Voice membership follows the tracked user in the configured channel, independently of the active profile; without a tracked ID it follows human occupancy. The legacy `gamingVoiceActivity` setting remains the opt-in switch. Streaming is a separate Gateway flag and does not depend on voice availability. Desktop expands Discord into the media area while the tracked user is in the connected roster (or the roster is nonempty without a tracked ID) and no media session is connected; paused sessions retain their panel. The header hides the tracked member when absent from the connected roster.

PresentMon collection is prepared before protected games launch. Discovery changes should not restart a healthy collector while a game is running. Metrics are application-presented FPS/frame times; status distinguishes unavailable or unprepared collection.

Google Calendar reads a DPAPI-protected iCal URL using an HTTP client with request logging disabled. Its fetch and Ical.Net recurrence evaluation run outside the render loop with one pending task, bounded feed/occurrence sizes and source-change cancellation. A pending response cannot overwrite a different calendar selection. URLs never enter API snapshots; only bounded appointment titles/times do.

Weather, news, lyrics and artwork use bounded asynchronous requests, cancellation and cache policies. They must not substitute unrelated stale content. More data-flow details are in [privacy](privacy.md).

## Rendering

`DeckRenderer` produces a 1920×480 BGRA frame and PNG preview from the same state/configuration. Desktop, Gaming and Music share widget resolution and drawing. Fixed-slot layouts retain hidden bindings when switching compositions. Music displays the first nine slots in a 3×3 grid, or the first three above a 24-band audio spectrum when `MusicSpectrum` is enabled. Hidden bindings are preserved.

The normal update target is roughly 1 Hz. During Aura following, notification arrivals and music playback, the renderer targets 10 Hz independently of the USB writer, which targets 4 Hz. A single immutable latest-frame reference replaces intermediate frames while USB is busy; no frame queue accumulates. Playback position is interpolated between provider samples using a monotonic clock, respecting playback rate and pausing, with extrapolation bounded to two seconds. This drives progress and synchronized lyrics without faster provider polling. Slow rendering or serial writes can reduce their respective cadences. Images are local or runtime-cached resources; proprietary artwork is not part of the source distribution.

The [documentation generator](../tools/docs-gallery/README.md) compiles this renderer with synthetic snapshots. It never calls production providers or writes to a panel.

## Display transport

`TurzxDisplay` validates VID/PID and HELLO identity before sending an initial full frame. `TurzxFrameDelivery` owns the acknowledged baseline and bounded recovery policy in portable Core code. Normal partial updates use the existing single changed bounding rectangle.
Aura, music playback and notification animations request complete frames at up to
4 Hz, using the verified full command. Higher-rate single and multiple-region
partial trials were rejected or timed out on the tested ROM; they are not the
production animation path. Unchanged frames still skip USB delivery. The
experimental `ChangedRegions` / `PartialFrameRegions` encoder is used only by the
[refresh diagnostic](../tools/display-refresh-probe/README.md); successful short
trials did not establish sustained reliability. Failed sends never become the
next baseline.

A `needReSend:1` reply invalidates the baseline and schedules a full frame. A recurring resend, timeout or malformed reply can require closing/reopening and revalidating the device. Recovery permits two attempts with minimum delays of 2 and 5 seconds, replenished after 60 consecutive acknowledged frames. It does not queue stale frames or loop indefinitely.

Explicit disconnect cancels pending recovery and startup reconnect attempts. Recovery does not wake standby devices; the explicit connection path handles the tested standby identity before validating the awake device. Shutdown blocks new work and attempts screen-off. No firmware is written.

The full-frame command `C8 EF 69 00 38 40 0E 10` is covered by regression tests. Protocol changes need actual device evidence. See [hardware compatibility](hardware.md) and [validation](validation.md).

`DisplayBrightness` is optional (null by default): existing configurations keep
the panel's current brightness. A saved integer from 0 to 100 sends the upstream
revision-C brightness command, scaled to 0–255. Control writes share the serial
gate with image delivery, apply after verified initialization/recovery, and are
deduplicated for a connection. A failed brightness write closes the connection
and reports an error rather than retrying continuously. Selecting null leaves
the currently applied brightness in place; it does not recover an earlier value.
With Aura following and a manual brightness configured, confirmed Aura OFF
temporarily sends 0% without changing configuration. Reactivation, unavailable
Aura or disabling Aura restores the saved brightness. Switching to inherited
brightness during this temporary override restores the last known manual value
once. `/api/display` exposes the last sent `AppliedBrightness` and override flag;
these are command diagnostics, not measured luminance.

## Local API and storage

The host binds to `127.0.0.1:5178`; SignalR `/live` publishes state snapshots and a separate `frame` revision whenever a preview PNG is rendered. Browser previews follow this revision rather than the one-second sensor cadence. Representative endpoints are `/api/state`, `/api/config`, `/api/preview.png`, `/api/sensors`, `/api/display` and `/api/games`. Mutations use the configurator client header and origin checks. This is a local trusted-user API, not a public authenticated service.
Overview and widget editing reuse a preview component that loads/decodes one
image at a time, keeps only the newest pending revision, retains the previous
picture until decoding succeeds, and releases retired blob URLs. A slower browser
therefore skips intermediate previews rather than cancelling each unfinished image.

Configuration, credentials and caches belong in `%LOCALAPPDATA%\PulseDeck` or `PULSEDECK_DATA_DIR`. Discord and SteamGridDB credentials use DPAPI CurrentUser and are not returned by API responses. Automatic startup is an elevated interactive-user task, not a service in session 0.

## Validation boundaries

Core tests cover portable parsing, selection and protocol state. Rendering tests exercise real Skia output and cache behavior. Extension and Electron tests cover their local policies. Build success does not prove Windows media access, accurate sensors, real speaking events or a correct physical picture; those require documented integration observations.
