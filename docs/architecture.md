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
bounding rectangle of subsequent pixel changes. Transport errors release the
port and require an explicit reconnect.

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
4. Tray lifecycle and explicit recovery after a later device disconnection.
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
