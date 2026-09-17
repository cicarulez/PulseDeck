# Validation — updated 2026-09-17

## Bounded USB recovery — 2026-09-17 (0.1.1)

- Initial repository clean at `bd40513`. Installed agent running hidden/elevated in
  interactive session 1, COM5 identified as `chs_88inch.dev1_rom1.90`; no TURZX
  process, legacy task disabled. The old build timed out during preparation;
  explicit connection restored output. Existing configuration/credentials preserved.
- 45 Core tests passed (27 existing plus 18 recovery cases). Deterministic fake
  transport/clock checks cover read/write timeouts, full-frame resend of the latest
  pixels, resumption of partial frames, invalid acknowledgements, failed reopen or
  enumeration, two-attempt exhaustion, intermittent failures, renewal only after
  60 acknowledged frames, and cancellation during backoff/reopen/transfer. Existing
  full-frame and shutdown packet regression tests remain unchanged.
- Self-contained Windows Release publish and Angular production build passed.
  SDK 10.0.401 and Node 24.15.0 used; PowerShell launcher parser check passed.
- Windows deployment stopped the agent and waited for both process and launcher
  exit before replacing files. App, configuration, encrypted credentials and task
  XML backed up under `%LOCALAPPDATA%\PulseDeck`, outside Git. The existing startup
  task was reused, with no task replacement. Configuration and credential SHA-256
  values matched before/after (values and credentials are not included here).
- Actual manual disconnection remained idle for 12 seconds: no new acknowledged
  frames; `connect?startup=true` returned 409 and did not reopen the port. Explicit
  connection then restored output. One hidden elevated agent, PawnIO available,
  616 sensor/parameter entries at this check, media and embedded Discord connected.
- Installed UI/preview checked at 1500x1000 and 390x844: no horizontal overflow,
  preview 1920x480, no page runtime errors. Recovery presentation additionally
  checked with an isolated browser API/WebSocket mock: **Scollega display** remained
  enabled and sent disconnect. The mock was confined to that test browser context;
  no simulated readings or fault-injection endpoints were added to the agent.
- **Real USB timeout recovered:** at 10:30 local time, the new agent timed out after
  80 acknowledged frames, entered `recovering`, reopened/revalidated COM5 and
  acknowledged a full frame on retry 1 (`recoveries=1`, frame 81). Subsequent live
  partial updates continued without an explicit connection command. API and agent
  logs agree; the retry budget reset after 60 further consecutive confirmations,
  with updates observed through frame 187. This is actual hardware recovery
  evidence, not an injected timeout.
- Final deployment also exercised normal stop: screen-off logged, old process and
  launcher exited, COM5 disappeared and only the identified standby COM3 remained.
  The reused hidden startup task opened standby COM3 and restored COM5; first frame
  confirmed at 10:32:56, about 23 seconds after agent startup. Final installed Core
  DLL matches the tested publish. Both deployment backups remain outside Git.
- The user visually confirmed the physical panel after recovery: complete, correct
  image and live data updates, without residual content or anomalous pixels. This
  complements the USB acknowledgements and preview checks above.
- Synthetic Windows session-end messages were not repeated for 0.1.1;
  their previous validation is recorded below. Shutdown while the port is already
  lost cannot send screen-off: recovery deliberately does not reopen during shutdown.
- `needReSend:1`, persistent failure exhaustion and cancellation during a recovery
  operation are covered by simulated transport tests; they have not yet all been
  observed on the physical device with this build. Physical cable unplug/replug,
  suspend/resume and actual Windows shutdown/logoff remain outstanding. No PC
  shutdown/restart, firmware change, ASUS control or TURZX launch was performed.

## Automated

- Windows agent Release build and self-contained win-x64 publish succeeded.
- Angular production build succeeded with strict template checking.
- 10 core regression tests passed: profile priority/debounce, manual override,
  exact process matching, backend URL validation, frame bounds, orientation,
  partial update addressing, and the full-frame command layout.
- Browser check: preview loads as 1920×480; live SignalR state is received.
- Overview and configuration tested at 390px width without horizontal overflow;
  no browser runtime errors observed on the final build.
- Configuration saved through the Angular form; manual Desktop selection was
  observed, then the previous automatic configuration was restored.
- Invalid configuration returned 400; API mutation without the configurator header
  and a request from a foreign browser origin returned 403.

## Real Windows integrations

- Hardware: CPU load, GPU load/temperature/power/VRAM, and RAM readings available.
- Missing CPU/motherboard/fan values were traced to absent PawnIO, even with an
  elevated agent. Installed official PawnIO 2.2.0 with user authorization; installer
  hash matched the release digest and Windows Authenticode validation was valid.
- After restart with administrator rights: 617 sensor/parameter entries, 615 with
  readings. Ryzen temperature, motherboard readings, 9 fan entries, RAM-module
  temperatures and SSD temperatures now available. Some temperature entries are
  device thresholds/resolution parameters, shown under their original names.
- Spotify: title, artist, playback progress and duration observed. Automatic Music
  profile selection confirmed, including subsequent track changes.
- Embedded Discord Gateway login and configured server/voice channel availability
  verified using imported DPAPI-protected credentials. Channel empty during testing;
  participant join/leave and mute/deaf transitions still need a live participant check.
- Config API exposes no token/secret/credential fields; invalid Discord mode returns
  400 and mutation without the custom client header returns 403.
- Sensor UI: search found 141 Ryzen entries; fan filter found 9; unavailable filter
  found 2; combined empty search state correct; 390px viewport has no page overflow.
- Library returned the same raw ID for NVIDIA GPU Bus and GPU Memory. Row tracking
  includes the sensor name to preserve both entries across live updates. Raw source
  IDs remain available unchanged. `/api/sensors` uses the live cached inventory.
- Final UI rendered all 617 rows with no page runtime errors; NVIDIA rows remained
  stable across updates. Repository scan found no imported bot token in source files.
- FPS and speaking activity remain unimplemented and are labeled accordingly.

## Physical display

The Python probe was confirmed visually: landscape orientation and the bottom-left
partial update were correct on `chs_88inch.dev1_rom1.90` (`COM5`, `0525:A4A7`).

The first C# run exposed a malformed full-frame command: an extra zero shifted
the frame dimensions, leaving old content and colored pixels visible despite a
success acknowledgement. The command was corrected from
`C8 EF 69 00 00 38 40 0E 10` to `C8 EF 69 00 38 40 0E 10`, and an independent
regression assertion against the successful Python probe's command was added.
The corrected build has been sent to the device and continues receiving status
acknowledgements. The user confirmed full-screen output without the previous
background or colored pixel artifacts. Live sensor/media updates continue.

## Startup integration — 2026-09-17

- Existing elevated interactive TURZX task `TempMonitor_8` exported to a local XML
  backup and disabled. No additional TURZX Run-key or Startup-folder entries found.
- PulseDeck task registered for the same Windows user with Highest privileges,
  Interactive logon, a 15-second delay and no execution time limit.
- Task launched manually to exercise the login action: one agent process, no main
  window handle, administrator sensors, embedded Discord connected, and automatic
  TURZX connection. Logs were redirected to files. No browser launch in the action.
- Full sign-out/sign-in and reboot were not performed; task trigger and settings
  inspected, actual startup action tested in the current interactive session.
- PowerShell parser validation passed for the new setup/background scripts and
  the updated interactive launcher.
- Intentional stop through the API completed the scheduled task with result 0,
  state Ready and zero remaining agent processes. Starting the task again restored
  the agent and display. This verifies the normal stop path does not signal a failure
  to the task scheduler's restart policy.

## Configurable widgets — 2026-09-17

- 19 Core tests passed, including legacy configuration defaults, sensor ID/name
  collisions, persistence round-trip, invalid bindings/scales, and bar fractions.
- Windows self-contained Release publish and Angular production build passed.
- Deployed after stopping and waiting for the previous process; published app and
  configuration backed up outside the repository. Existing startup task reused.
- Browser selected the real Nuvoton Fan #2 for bar 1, label `VENTOLA #2`, scale 3000.
  Draft edits did not change the live configuration until Save. A zero scale disabled
  Save; the API independently rejected it with 400. Saved bindings were read back.
- Hide and restore-defaults workflows verified. Original widget configuration restored
  after testing; music, Discord, display and other settings preserved.
- Shared preview remains 1920x480. Widget page inspected at 1500x1000 and 390x844;
  no horizontal page overflow. Live fan preview and label rendered correctly.
- Elevated hardware inventory still contains 617 entries; embedded Discord connected.
- During checks the display reported `needReSend:1`; the existing transport closed
  the connection on that error. Explicit reconnect succeeded, then the fan layout
  and restored defaults were sent with connected status. Automatic full-frame recovery
  remains follow-up work. No new user visual confirmation of this layout yet.

## Aura read-only feasibility — 2026-09-17

- Local ASUS Aura SDK 3.07.05.0, registered COM `aura.sdk.1`, queried in a separate
  Windows PowerShell process. No SwitchMode, LED setters or Apply calls; ASUS
  services and hardware illumination left under their existing controller.
- Standard-user process exited with native code 9 while enumerating. An elevated
  process enumerated devices and read three samples, with at most 12 devices and
  three LEDs per device. This is not evidence of complete-device coverage.
- User confirmed a static yellow effect. Returned RGB values did not match it and
  remained unchanged; some looked like invalid/stale buffer contents. No dynamic
  effect test was attempted after this failed static comparison.
- No Aura provider enabled in PulseDeck. Manual accent remains the supported path;
  this result does not rule out other independently validated passive data sources.

## Aura passive effect-file inspection — 2026-09-17

- Read local `LightingService/script/LastScript.xml` as XML without invoking the SDK
  or modifying ASUS files/services. It described 11 static effects, all with HSL
  `(0.166667, 1, 0.5)`, converting to yellow `#FFFF00`, with `OneTime` triggers and
  `ConstantWave` entries. This matches the user's earlier static-yellow observation;
  no new physical color comparison or controlled color change was performed.
- `LightingService/LastProfile.xml` instead contained color `255` and hue `0` in
  the group profile. A persisted profile is not sufficient proof of active LED colors.
- This is a candidate passive source, not a validated provider. Still required:
  correlate file changes with user-selected static colors, identify when persisted
  state is stale/inactive, and assess dynamic-effect representation. Agent unchanged;
  no proprietary files copied into Git.

## Aura virtual HAL discovery — 2026-09-17

- Added `tools/aura-probe`, independent of the agent. Metadata inspection uses
  `LoadTypeLibEx(REGKIND_NONE)`; local ASUS libraries and inspection output remain
  outside Git. SDK 3.07.05.0; GmAcc HAL 1.0.12.0.
- Default probe passed in a non-elevated, isolated 32-bit Windows PowerShell child:
  `EumerateHalInfo` returned exactly our private GUID and `CreateHal` invoked our
  COM factory once. The prototype exposes the standard `IAacLedDeviceHal` contract,
  obtained from the installed DRAM type library. No physical device was enumerated.
- Actual device enumeration failed: the explicit `-EnumerateDevices` experiment
  exited with `0xC0000005` before the managed enumeration callback. Application
  Error records identify `clr.dll` 4.8.9345.0; root cause is unresolved. Changing
  the registry view back before activation did not resolve it. Default probe
  deliberately excludes this operation; discovery/activation passed with exit 0.
- Parent enforces a 20-second limit and cleans the uniquely named private HKCU tree
  after normal exit or child failure. HKCR redirection and COM registration are
  process-local. No persistent HAL registration or change to ASUS configuration,
  no service restart/stop, no `SwitchMode`, `Apply`, LED setters or signature bypass.
- Static inspection found GmAcc's virtual branch and its connection to loopback
  11000. The port is already owned by `Aura Wallpaper Service`; the virtual branch
  precedes the Wallpaper branch. Global flags were left unchanged to preserve it.
- **Not verified:** discovery by the running LightingService, Armoury Crate tile,
  live color delivery, dynamic effects, or a new physical RGB comparison. An SDK
  factory activation is not evidence that the service accepts third-party HALs.
- PulseDeck remained on version 0.1.1. At the start of this investigation it had
  exhausted USB recovery; a normal connect restored COM5. Later observation showed
  another timeout recovered within budget, with acknowledged frames progressing.
  No new physical-panel confirmation was requested during Aura work.

## Aura native virtual-device enumeration — 2026-09-17

- Replaced the disposable managed host/HAL with our own native C x86 executable,
  cross-built with MinGW-w64 GCC 13.2.0 (`-Wall -Wextra -Werror`). No vendor code or
  binaries copied into the repository; build output and diagnostics remain local.
- Keeping an extra COM reference did not resolve the old crash. A native HAL
  called from the managed host still crashed; the fully native host/HAL passed.
  This avoids the failing managed path, without establishing its exact root cause.
- **Actual SDK device enumeration passed:** three consecutive `EumerateDevices`
  calls returned exactly one `PulseDeck Virtual Probe`, width/height 1x1 and one
  LED. Native callbacks recorded one factory activation, six enumeration calls
  (count/data phases) and three capability reads. All names, counts, dimensions,
  expected GUID and variant types are asserted by the executable.
- Empty HAL case passed: three enumerations, zero devices, zero capability reads.
  Neither case requested an effect or synchronization callback. No RGB getter,
  setter, Apply or SwitchMode invoked; the virtual device advertises no effects.
- Reference lifetime remains a limitation: after releasing SDK objects and
  uninitializing COM, the one-device case reported HAL=5/device=4/factory=1; empty
  case 2/1/1. Each singleton retains one intentional root reference. These counts
  are reported, not hidden by extra Release calls. Each probe process fully exits;
  this is not evidence that a persistent host has bounded resource use.
- Supervisor failure tests used a separate local fixture: child exit 7 and a
  one-second timeout both reported failure and left no child or private registry
  key. The native executable rejected `Software\ASUS` as a scratch path with exit
  2. Final checks found zero probe processes and zero scratch-key children.
- Existing LightingService and ArmouryCrateService stayed Running with unchanged
  PIDs. Configurator HTTP 200; display connected on the existing agent 0.1.1, with
  acknowledged frames advancing from 1957 to 2687 during this work. No deployment,
  display reconnect, ASUS setting change or service restart was needed.
- **Still unverified:** registration/discovery in the live ASUS service, appearance
  in Armoury Crate, actual color reception, dynamic effects and physical LED color
  matching. No user action in Armoury Crate is required for this isolated milestone.

## Aura receiver isolation and lifetime — 2026-09-17

- Direct checks of our native COM contracts passed 100 cycles, covering both
  enumeration return shapes, BSTR/interface/SAFEARRAY cleanup and root reference
  counts of 1/1/1 after each cycle. No ASUS SDK loaded in this check.
- SDK enumeration passed 30 iterations but retained HAL/device/factory references
  of 32/31/1 at exit, versus 5/4/1 after three. This isolates the excess to the SDK
  integration path; it does not establish the vendor's ownership contract or a fix.
- Direct cross-process COM experiments failed (zero SDK devices and a native
  adapter stack-overflow). They ran only in disposable probes. Chose a private
  IPC receiver while keeping SDK/HAL together in the existing native test host.
- Receiver uses anonymous shared memory/events with an explicit inherited handle
  list. Its job is kill-on-close and assigned before its suspended thread resumes;
  it also has a 15-second lifetime limit. No ASUS code or hardware calls in receiver.
- `-SeparateProcess` passed with one SDK device, no samples and source unavailable;
  the empty-HAL variant passed too. `-TestTransport` passed two exact synthetic
  pattern/acknowledgement checks with zero effect/synchronization requests. Output
  labels samples synthetic. Incoming HAL callback is wired to forward a single raw
  word as unverified, but was never invoked; no effect is advertised. Synchronization
  remains E_NOTIMPL. **No actual Aura colors received.**
- Real process-tree timeout test observed two native processes, then forced the
  supervisor's three-second timeout during a ten-second observation interval.
  Both native processes exited and the supervisor removed its private registry key.
  This expected-failure test complements the normal exit checks.
- Compiled with warnings treated as errors. Agent installation, configuration,
  startup tasks and ASUS service settings unchanged. No LED setter, Apply,
  SwitchMode, service restart, system HAL registration or physical RGB test.

## Aura effect descriptor and running-service read — 2026-09-17

- Native probe advertises one Static effect, ID 1, with synchronization disabled.
  SDK getters verified its count, name, ID and synchronization flag on three device
  enumerations. Callback accepts only ID 1 and a single raw word; unsupported IDs
  return E_NOTIMPL. No invocation of this incoming callback was attempted.
- Rebuilt with `-Wall -Wextra -Werror`. Direct COM ownership checks passed 100
  cycles; separate receiver, empty HAL and two synthetic transport samples passed.
  All SDK discovery runs retained zero effect/synchronization requests. Color
  encoding, live delivery and physical matching remain unverified.
- Added a separate, bounded read of `IServiceMediator.get_QueryAllDeviceCap`,
  using the running LightingService's COM local server. Windows call succeeded;
  response is valid XML, includes Aura Wallpaper entries and no PulseDeck name.
  This is capability metadata, not LED readings or proof of every listed location
  being physical hardware. Full response stays in local runtime storage outside Git.
- LightingService stayed running with PID 6840. Static inspection found that its
  manual refresh path tears down effect executors; it was not called. The signature
  check inspected in DoEnumerateHalInfo targets AuraSdk_x86.dll, not evidence of a
  blanket third-party HAL rejection. No signature checks were bypassed.
- No live HAL registration, Armoury Crate tile, ASUS color callback or persistent
  provider installed. COM isolation across processes remains unresolved; the private
  IPC receiver alone does not make the HAL discoverable outside its test host.

## Windows shutdown and display power — 2026-09-17

- Root cause: the runtime only released the serial port at exit, and the hidden
  console agent did not explicitly listen for Windows session-end messages.
- Added a dedicated invisible top-level window, synchronous screen-off before
  acknowledging confirmed session end, and the same shutdown path for normal stop.
  A cancelled query leaves the app and display running. No visible UI/service added.
- 27 Core tests passed, including exact 250-byte screen-off/stop packets and rejection
  of unrelated standby device identities. Self-contained Windows Release publish passed.
- `scripts/Test-PulseDeckSessionEnd.ps1 -EndSession`, run elevated on Windows, sent
  query/cancel/confirmed-end messages only to PulseDeck, without shutting down Windows.
  The window was invisible; query returned TRUE; cancellation kept USB connected;
  the confirmed-end handler returned in 3 ms and the agent exited.
- User visually confirmed the physical screen and backlight turned off.
- The device changed from COM5 / `0525:A4A7` to COM3 /
  `USB\VID_1A86&PID_CA88\CT88INCH`. This exposed the need for standby wake detection.
  Opening that identified standby interface with RTS/CTS restored the awake device.
- Normal `/api/stop` also logged a successful screen-off command and exited. The
  Windows startup task was reused after deployment, with settings/credentials intact.
- Final build automatically opened standby COM3, waited for re-enumeration, and
  connected to the identified COM5 display via the existing bounded startup retry.
  Task log: agent started 09:49:48, display connected 09:49:59 (about 11 seconds).
  One hidden elevated agent remained active with hardware readings and Discord connected.
- Actual Windows shutdown/restart and logoff were not performed. The synthetic
  message check verifies the handler and hardware command, not Windows shutdown
  ordering on this PC. Forced termination, USB errors and power loss remain outside
  the guarantee of a cooperative shutdown handler.
