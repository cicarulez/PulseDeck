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
on the open port. Timeouts, malformed replies and I/O failures close the port;
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
fields; the configuration schema remains unchanged. Angular's display controls
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

The current render cadence targets one update per second; slow providers or
serial writes reduce that cadence. There is no unbounded frame queue. Smooth
animation is a separate performance milestone, not implied by the static probe.

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
3. Album artwork and media-driven theme variations.
4. Tray lifecycle and physical unplug/replug and suspend/resume validation of bounded USB recovery.
5. Extend the fixed-slot sensor editor to per-display themes and layouts. Add video only after throughput
   and CPU/GPU overhead measurements on the actual display.

Theme images remain local user resources. The existing Earth theme and potential
Battlefield assets are not necessary for the engine and are not bundled here.

## Configurable sensor widgets

`WidgetCatalog` defines eight stable slots, their default bindings and validation.
`DeckConfig.widgets` is additive to schema 1: older files receive the original layout
through the property initializer. Bindings select a summary metric, a raw sensor by
ID plus name, or no content. Missing sensors remain bound and render as unavailable.
Bar maxima are explicit, positive finite values; displayed readings are not clamped.

`/api/widget-slots` supplies the slot catalog/defaults to Angular. The standalone
widgets page owns an editable draft and delegates selection to a slot editor;
the existing config endpoint validates and persists all eight slots together.
The renderer resolves bindings against the cached snapshot, scales long values
and constrains labels to the slot width. Music and Discord keep their fixed areas.

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
HAL effect callback can forward one raw word as unverified; no effect is advertised
and no such callback has been observed. The probe itself never invokes LED setters.

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
