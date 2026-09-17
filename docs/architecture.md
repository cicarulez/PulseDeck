# Architecture and next milestones

PulseDeck separates data acquisition, state selection, rendering and transport.

`HardwareProvider`, `MediaProvider` and `DiscordProvider` produce typed snapshots.
`ProfileSelector` gives a foreground game priority over playing media, then falls
back to Desktop. A new automatic candidate must remain selected for the configured
delay; manual selection applies immediately. Discord stays visible in every profile.

`DeckRenderer` creates an opaque BGRA image. The same result is encoded as PNG for
the browser and passed to `TurzxDisplay` for USB serial output. Angular edits
settings and displays that PNG; it does not duplicate the theme rendering rules.
The native adapter validates VID/PID and the device ID, initializes the display
without writing startup options, sends an initial full frame, and computes the
bounding rectangle of subsequent pixel changes. `TurzxFrameDelivery` owns the
acknowledged frame baseline and bounded recovery policy in portable Core code.
A complete `needReSend:1` reply invalidates that baseline and schedules a full frame
on the open port. If a resend recurs after the first attempt (even after an
acknowledged full frame), the second attempt closes/reopens and reinitializes the
identified connection, resetting its partial counter. This uses the existing retry
budget rather than adding attempts. Timeouts, malformed replies and I/O failures
close the port;
recovery revalidates VID/PID and the original HELLO identity before reinitializing.
Recovery never wakes standby devices. Explicit connection retains the wake path.

Retries use monotonic time on later render ticks (at least 2 then 5 seconds), using
the newest pixels, with no sleeping retry loop or frame queue. Two attempts are
allowed until 60 consecutive acknowledged frames restore the budget. One successful
recovery cannot create an endless intermittent-error loop. Exhaustion releases the
port and requires explicit connection. A failed frame is never a partial-update base.
The existing 2-second read and 10-second write limits still apply per operation;
backoff does not impose an overall operation deadline or isolate the render loop.

The Windows adapter serializes connection, delivery and shutdown. A generation
change invalidates in-flight delivery when an explicit connect/disconnect arrives;
shutdown sets its stop flag before waiting for the USB lock. Cancellation is checked
between operations; an already running serial call retains its timeout. There is no
background recovery task to resurrect a disconnected/stopping device. The startup
launcher uses `connect?startup=true`, which respects manual disconnection and active
recovery. Diagnostic counters and the last acknowledgement/error are additive API
fields, including last attempted frame kind/partial counter in 0.2.5; the
configuration schema remains unchanged. Angular's display controls
show recovery status and keep Disconnect available while recovery is pending.

`WindowsSessionLifetime` owns an invisible top-level Win32 window on a dedicated
message thread. It accepts `WM_QUERYENDSESSION` immediately and acts only on
`WM_ENDSESSION` with a nonzero wParam, leaving cancelled shutdowns untouched.
Before returning that message, it synchronously asks the display to stop media and
send the protocol's screen-off command, then requests host termination. Console
shutdown handlers alone are insufficient because this process loads user32.

Normal host stop uses the same idempotent path. New frames/connects are rejected
once shutdown starts; access to serial output stays serialized with in-flight
frames. Lock acquisition and serial operations have short timeouts. Failures are
logged without blocking Windows indefinitely. Explicit user disconnect remains a
port release and is not treated as a request to power off someone else's display.

On the tested firmware, screen-off re-enumerates as `USB\VID_1A86&PID_CA88\CT88INCH`
on COM3. Connect first checks the configured awake port; if absent, it only wakes a
unique matching standby identity by opening its interface with RTS/CTS, following
the upstream wake strategy. It then waits up to five seconds for the configured
awake port and performs the normal VID/PID and HELLO checks before sending frames.
No generic serial devices or other display revisions receive wake commands.

Protocol reference: [upstream ScreenOff](https://github.com/mathoudebine/turing-smart-screen-python/blob/main/library/lcd/lcd_comm_rev_c.py).
Windows references: [console handler limitations](https://learn.microsoft.com/en-us/windows/console/setconsolectrlhandler),
[WM_QUERYENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession),
[WM_ENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-endsession).

The agent runs in the interactive user session so that foreground windows and
Windows media sessions are available. Hardware data availability depends on
permissions and the hardware's sensor support. Values are never fabricated.

Rendering and shared provider acquisition target one update per second. Serial delivery
is still synchronous, so slow providers or writes reduce cadence; no frame queue is
introduced. The animated-background experiment was retired at the user's request in
0.2.1: no animation clock, retry guard or fast render cadence remains in the agent.

## Sensor inventory and embedded Discord

Hardware enumeration includes CPU, GPU, memory, motherboard/subhardware, storage,
network, controllers, battery, power supplies and power monitors supported by
LibreHardwareMonitor 0.9.6. `/api/sensors` returns the same cached hardware snapshot
sent by SignalR, avoiding concurrent hardware polling. Original sensor identifiers,
raw finite values, units and library session min/max are preserved. Per-device
update failures are reported without discarding healthy devices. PawnIO detection
and Windows administrator status are separate diagnostics.

`EmbeddedDiscordService` owns the Discord.Net Gateway client in the agent process.
`DiscordProvider` selects embedded or optional legacy HTTP snapshots. The embedded
client reads the configured voice channel from its event-updated cache, reports
readiness, and clears visible membership while disconnected. No audio transport or
message handling is enabled. `Import-Discord.ps1` handles migration outside the
agent; DPAPI CurrentUser protects the resulting local credential file. API config
contains the integration mode and tracked user, never the token.

## Next increments

1. PresentMon adapter: correlate the foreground game's PID and expose clearly
   named application/display FPS and frame-time metrics.
2. Discord speaking proof: test a bot voice connection in the target server;
   keep unsupported/offline activity separate from silence and mute.
3. Extend static themes and investigate installed-game discovery from reliable local sources.
4. Tray lifecycle and physical unplug/replug and suspend/resume validation of bounded USB recovery.
5. Extend the fixed-slot sensor editor to per-display themes and layouts. Add video only after throughput
   and CPU/GPU overhead measurements on the actual display.

Theme images remain local user resources. The existing Earth theme and potential
Battlefield assets are not necessary for the engine and are not bundled here.

## Configurable sensor widgets

`WidgetCatalog` defines sixteen stable slots, default bindings and validation.
`DeckConfig.widgets` is additive to schema 1: older files receive the original layout
through the property initializer. Exact original eight-slot sets are accepted and
expanded on load/save with eight hidden slots; malformed/incomplete sets are rejected.
The compact layout is the new default; the classic layout retains the original eight
positions without discarding extra bindings. Bindings select a summary metric, a raw sensor by
ID plus name, or no content. Missing sensors remain bound and render as unavailable.
Bar maxima are explicit, positive finite values; displayed readings are not clamped.

`/api/widget-slots` supplies the slot catalog/defaults to Angular. The standalone
widgets page owns an editable draft and delegates selection to a slot editor;
the existing config endpoint validates and persists all sixteen slots together.
The renderer resolves bindings against the cached snapshot, scales long values
and constrains labels to the slot width. Compact cards support values, bars and rings.
RAM used obtains capacity from physical `/ram` used+available readings, never `/vram`;
unknown capacity does not become a fictitious 100 GiB total. Network widgets bind a
specific interface sensor (including its name), with adaptive B/s/KiB/s/MiB/s display
and original byte-based scales. Music and Discord keep dedicated areas in each layout.

## Weather acquisition and twelve-sensor composition

Schema 1 adds nullable `weatherLocation` (name/latitude/longitude) and layout
`weather`; legacy configuration makes no weather network request. The weather
layout retains the sixteen stored widget bindings but displays the first twelve in
a 3×4 grid. A fixed left weather card and the existing right media/Discord card use
the same renderer for preview and USB, including Gaming. Game foreground identity
and per-game backgrounds remain independent of that composition.

`WeatherFeed` is owned by the render loop and polls a single asynchronous request
without awaiting it on the render path. Open-Meteo requests use a fixed HTTPS host,
explicit units, five-second cancellation budget and a 64 KiB response limit. Success
is cached for fifteen minutes, failure for two. Location/enable changes cancel and
discard any pending result; failures clear measurements, old responses cannot
overwrite another city. Model timestamps older than two hours or over thirty
minutes in the future are rejected. Cached data is discarded after thirty minutes
without a successful fetch, including suspend/clock discontinuities.

`GET /api/weather/locations?query=...` proxies a bounded eight-result Open-Meteo
geocoding query. The settings feature owns search/results and emits the selected
coordinates through `locationChange`; saving uses the existing config endpoint.
There is no IP/GPS lookup, external credential, game write or browser-dependent
weather polling. Weather state includes model time (location UTC offset) and fetch
time. Since 0.2.6, publisher attribution and the model-estimate explanation remain
in the configurator rather than a small credit line on the display. Humidity and
wind occupy separate 20 px lines. Since 0.2.7, the model-time
line is also omitted from the panel; time remains available in the API state.
No physical PC weather sensor is implied.

References: [forecast API](https://open-meteo.com/en/docs),
[geocoding and GeoNames attribution](https://open-meteo.com/en/docs/geocoding-api).

## Media artwork and static backgrounds

`MediaProvider` reads the selected session's Windows `Thumbnail` (the same Spotify-first
selection as metadata). `MediaArtworkCache` retains one bounded, normalized PNG in
memory. Identity includes app, title, artist, album and track number; session/identity
changes clear it before reading. Successful entries refresh after 30 seconds, missing
or failed ones after 5. Thumbnail reads have a one-second cancellation budget; failures
do not discard usable text metadata. Inputs are capped at 4 MiB and 4 megapixels,
normalized to at most 256 pixels on either side, and identified by a content hash.
Only `artworkId` goes into state/SignalR; image bytes go directly to the renderer,
which caches its decoded image and refuses artwork whose ID differs from the snapshot.
Neither media artwork nor user assets are persisted or bundled by this provider.

Reference: [Windows media properties and Thumbnail](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmediaproperties?view=winrt-26100).

`StaticBackground` caches only one decoded image, invalidated by path/modification time.
Inputs are limited to 32 MiB and 4 megapixels. Legacy GIF/animated WebP files decode
only frame zero; unknown legacy `animateBackground` JSON is ignored without resetting
other configuration. Missing/invalid files clear the cache and leave the built-in dark
Skia gradient with a warning. `/api/rendering` still reports status and count (0 or 1).
No user artwork is bundled. The rendering tests link the real renderer/cache classes
and synthetic images, including static handling of old GIFs and stale-icon rejection.

## Foreground identity and executable icon

The 0.2.2 configuration adds `gamingLayout` (default true) and `gameThemes` (empty
by default) without changing schema 1. Theme entries map exact process names to
local background paths; case/extension-equivalent duplicate keys are rejected.
They do not register a game: only the existing `gameProcesses` rules classify it.
Configuration writes check local files/extensions; the existing bounded static
decoder enforces byte/pixel limits and clears failed or previous images.

`GameSceneSelector` produces the separate nullable `DeckState.game` after profile
selection. It retains a known game's presentation only during the bounded automatic
Alt-Tab delay, clears it on profile exit/manual non-game/rule removal, and switches
immediately between recognized games. `foreground` always describes actual focus.
The renderer retains only that scene's matching game icon, separately from the
foreground icon; no executable assets are persisted. The Gaming composition reuses
the compact widget-card and media/Discord drawing helpers with narrower cards,
preserving all sixteen bindings. A GPU text badge comes from real hardware identity;
neither vendor artwork nor a new NVIDIA provider is included.

`TrackedVoiceHeader` resolves the configured ID against the current connected
Discord roster, refusing stale `Tracked` snapshots and unrelated members. It exposes
mute/deaf or explicit unavailable states. Rendering constrains the nickname/status
to a separate header area before the clock; it makes no speaking inference.

`ForegroundProvider` reads the current HWND/PID and disposes each `Process` handle.
It obtains the executable path internally and extracts file description and associated
icon with Windows/.NET APIs. It does not read window titles, attach to games, inject
code or load their executable as a running program. Full paths are not exposed by API.
A bounded 32-entry cache retains names/PNGs, refreshing after 60 seconds (10 on missing
icons). Extracted icons and bitmaps are disposed after PNG encoding. Protected processes
retain their process name where available; lookup failures clear the current icon.

`DeckState.foreground` adds PID, process/display names, game classification, icon hash
and availability. Existing `foregroundApp` remains compatible. `ProfileSelector.IsGame`
provides the same exact process-name matching for badge and automatic profile selection;
the badge follows actual foreground regardless of manual profile or profile debounce.
The renderer accepts PNG bytes directly and displays them only for a matching snapshot
hash. Alt-Tab updates the app identity on the next tick; no old game icon is retained.

References: [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow),
[Icon.ExtractAssociatedIcon](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.icon.extractassociatedicon?view=windowsdesktop-10.0).

Aura SDK passive sampling has not produced trustworthy live colors on the tested
installation. It remains outside the runtime; see the evidence in the roadmap.
The Aura experiment now hosts the SDK and a minimal virtual HAL in a disposable
native x86 process. It validates discovery and device metadata using a process-local
registry view; it does not register a device with the running ASUS service. The
PowerShell parent enforces a deadline and removes its private registry tree. SDK
references retained after enumeration are reported and contained by process exit;
live RGB reception and a continuous receiver remain unverified. A second native
process now exercises a private latest-value IPC transport using inherited shared
memory/events; it does not load ASUS code. The SDK host owns a kill-on-close job
containing that receiver, assigned before resuming its first thread. The test mode
labels its two patterns synthetic, while normal mode reports unavailable. The incoming
HAL effect callback can forward one raw word as unverified for effect ID 1 only.
The SDK recognizes its Static descriptor, with synchronization disabled. No such
callback has been observed. The probe itself never invokes LED setters.
An independent bounded mode reads `IServiceMediator.get_QueryAllDeviceCap` from
the already-running LightingService. It saves the response outside Git and never
registers a HAL or requests a device refresh. Service capabilities are metadata,
not current RGB samples; PulseDeck is absent from that response on the tested PC.

`Test-ComIsolation.ps1 -WithSdk` now verifies a separate native COM server through
temporary `CoRegisterClassObject(CLSCTX_LOCAL_SERVER)` registration. It does not add
Classes or ASUS category registry entries. The STA server pumps messages, observes
supervisor exit/stop events and has a 15-second deadline. The SDK client discovers
only our private HAL category and restores normal HKCR before external activation.
Its Enumerate2 path requires IAacLedDeviceOpt2, so the device exposes the inherited
Opt/Device2/Opt2 interfaces with their correct vtable slots. Their extra incoming
effect callbacks return E_NOTIMPL; no live color reception is claimed. Direct COM
and SDK metadata checks pass, including class-table cleanup after forced server exit.
The separate SYSTEM-client check now also proves cross-user/session activation:
temporary CLSID-to-AppID mapping and AppID RunAs=Interactive User allow a native
client in session 0 to reach the existing user-session server. No LocalServer32
autostart command, credentials or machine-wide COM security defaults are changed.
The helper uses a unique, bounded SYSTEM task; the PulseDeck agent stays interactive.

`Test-SystemComIsolation.ps1` elevates only its registration/task supervisor. Baseline
mode changes no COM registry entries; `-InteractiveIdentity` owns two temporary keys
and refuses to replace existing ones. `-WithSdk` runs private-category SDK checks as
SYSTEM after the direct test. Optional `-ObserveAuraSeconds` publishes just our HAL
category entry after both checks pass, then reads existing service capabilities.
It removes its category/class/AppID keys and task in finally and verifies absence.
Runtime audit files live under LOCALAPPDATA, passed explicitly to the SYSTEM phase.
Native server lifetime remains 15 seconds by default, configurable up to 120 for
this bounded workflow; supervisor exit still ends it. Passive publication for 30
seconds did not cause live service activation. No provider or remote color handling
is enabled by these checks; added effect callbacks still return E_NOTIMPL.

## Windows startup

The elevated interactive-user scheduled task runs a hidden PowerShell launcher.
The launcher starts the agent with redirected stdout/stderr, waits for its health
endpoint, and attempts a bounded initial USB connection. It stays attached to the
agent it owns and propagates its exit code so unexpected failures can be retried.
An existing healthy agent is reused. The task uses IgnoreNew, has no execution time
limit, and runs only in the configured user session. No browser is opened.

The installer exports existing task XML before changes, registers and verifies
PulseDeck before disabling the legacy TURZX task, and rolls back the task changes
if setup fails. Hardware/runtime files and credentials are untouched by setup.

## On-demand experimental Aura HAL

`PulseDeck.AuraHal.exe` is a separate x86 GUI-subsystem COM host with no ASUS SDK
loading or outgoing RGB calls. Its machine x86 `LocalServer32` registration uses
the launching client's identity, allowing a SYSTEM caller to start it in session 0
without an interactive-user host. This does not change the PulseDeck agent identity.
The executable and helper scripts live in an administrator-owned Program Files
folder with explicit SYSTEM/admin write and Users read/execute permissions.

The STA pumps messages and exits after 15 idle seconds once HAL/device/factory
references and server locks allow it. Class suspension prevents new SCM activation
during final idle shutdown. A removal marker in the protected installation folder
ends only this host; the remover first unpublishes its owned category and CLSID.
One latest JSON report per account/session records activation counts, callbacks and
exit state under that account's LOCALAPPDATA/PulseDeck. SYSTEM x86 writes to the
SysWOW64 systemprofile location; no log or vendor binary goes into the repository.

`Manage-InstalledProbe.ps1` refuses existing registration/install paths, records an
ownership manifest and file hashes, tests on-demand SYSTEM COM/SDK activation and
idle exit before publishing the Aura category, and rolls back handled failures.
The temporary SYSTEM test task is removed. A persistent installation conflicts with
the earlier temporary-class/absence tests: remove it before running those tests.
No color reception, service acceptance or reboot behavior is established by the
SYSTEM client test. This prototype still returns E_NOTIMPL for effect callbacks.

The detailed read-only service inventory now uses a separate fixed getter,
`get_QueryAllDevice`, rather than interpreting absence from the aggregate capability
XML as device absence. Matching uses our manufacturer/model. The HAL advertises
EXTERNAL_GENERAL (0x64000), as mapped in the installed service, instead of type 0
(All). Name and model both identify PulseDeck. Incoming callback diagnostics capture
only method/effect/count/VARTYPE, not colors, while unsupported callbacks continue
to fail explicitly. Neither a registered device nor this generic classification
guarantees an Armoury Crate tile or selection in the user's sync group.

The observed incoming SetEffect2 contract (effect 0, one VT_UI4 SAFEARRAY element)
now has a passive sink. A separate decoder validates non-BYREF VT_ARRAY|VT_UI4,
rank one, ULONG element width/type, count one and matching bounds before reading.
It accepts a nonzero lower bound, never frees caller-owned input, and copies only
one word. The callback stores a bounded latest sample with monotonic tick/count and
returns S_OK; it does not call an SDK method or affect hardware. Other modes remain
unsupported. Diagnostics explicitly mark colorVerified=false; this is not a runtime
AuraColorProvider or proof the device participates in the user's sync selection.

## RSS/Atom news footer

Schema 1 adds opt-in `news` settings: enabled, rotationSeconds (10–120, default 20),
fontSize (20–26 pixels, default 24), plus up to eight named HTTPS channels with
independent enable flags. Legacy configs keep the previous footer and perform no news requests. Presets live in the news
settings feature component; adding a preset populates the same editable channel
model used by custom feeds. Settings changes apply only after the existing save.

The single render-loop owner polls `NewsFeed.Read` without awaiting network I/O.
Each refresh launches at most eight bounded requests concurrently; eight-second
cancellation and a 512 KiB decompressed response cap apply to each feed. Success is
cached fifteen minutes, any failure retries after two; failed sources lose their
headlines while healthy sources remain available with aggregate `partial` status.
A selection change cancels/discards the old batch; data older than thirty minutes
since request start clears after suspend. Disabling cancels polling and clears state.

RSS 2.0 and Atom parsing prohibits DTD/external entities, bounds XML characters,
strips markup/control characters from titles, rejects non-web article links and
retains at most ten unique items per channel. Known dates older than seven days or
more than thirty minutes in the future are excluded; missing/unparseable dates
remain explicitly unknown. Sources are interleaved before global link deduplication.
Only title, source, article link and publication time enter state; no article body,
remote image, script or attachment is fetched. Cache stays in memory, outside logs.

The agent HTTP handler uses no proxy, cookies, credentials or automatic redirects.
Only direct public HTTPS feeds on port 443 are accepted. DNS addresses are checked
and the connection uses the selected public address directly, avoiding a second
DNS lookup and local/private destinations. Feed errors are status-only in the API.
The renderer replaces only y=446..479 with source, one headline and publication time;
rotation is tied to snapshot time and configured interval, never a smooth marquee.
The configurator exposes source status and complete headlines linked to publishers.

## Electron desktop shell

`apps/desktop` packages the existing Angular configurator in an unprivileged
Electron window at the fixed agent origin `http://127.0.0.1:5178`. There is no
preload/IPC bridge, Node integration or duplicate data/USB owner. Context isolation,
sandbox, denied permissions/downloads and a local content security policy apply.
Navigation stays on the exact agent origin. HTTPS links require confirmation and
open in the default browser; arbitrary URL schemes are rejected.

The desktop checks `/api/health`, optionally runs the existing `PulseDeck`
scheduled task once, then waits a bounded interval. It never launches an elevated
Electron instance or reconnects a deliberately disconnected display. Closing the
window exits Electron alone. A second invocation focuses the existing window.
The configurator's explicit Stop button still stops the agent. Desktop binaries
live in `%LOCALAPPDATA%\PulseDeck\desktop-app`; Chromium user data lives in
`%LOCALAPPDATA%\PulseDeck\desktop`, independently of the agent's config/credentials.

Build and install scripts are separate from the agent package; updating only this
shell requires closing Electron, not replacing any running agent DLL. The package
currently has no signing, installer wizard or automatic update mechanism.
Reference: [Electron security](https://www.electronjs.org/docs/latest/tutorial/security).
