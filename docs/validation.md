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

## Aura native COM server isolation — 2026-09-17

- Added a temporary native EXE server registered in COM's running class table,
  without persistent Classes or ASUS category registry changes. A direct client
  successfully called Enumerate2, queried IAacLedDeviceOpt2 and read GetCapability
  through the installed Automation proxy. Server and client are separate processes
  in the same Windows user/session; no ASUS service code hosts our implementation.
- Initial SDK client failed with access violation 0xC0000005 at AuraSdk_x86.dll
  offset 0x16b92. Inspection showed its Enumerate2 path requests IAacLedDeviceOpt2;
  failure left no device-pointer array before the later access. Exposing the actual
  inherited Opt/Device2/Opt2 interface layout fixed this reproducer without changing
  the SDK or returning a mismatched interface pointer. Their extra effect callbacks
  remain E_NOTIMPL and are never invoked by the test.
- `Test-ComIsolation.ps1 -WithSdk` passed direct metadata checks plus three SDK
  enumerations: name, 1x1 dimensions, one LED and Static effect. Server recorded
  two activations, seven Enumerate2 calls, four capability reads and zero effect or
  synchronization calls. Client local HAL counters stay zero, as asserted.
- Normal shutdown and `-WithSdk -TerminateServer` both left the class unavailable:
  subsequent CoGetClassObject returned REGDB_E_CLASSNOTREG (0x80040154). Forced
  termination affects only our probe server. No PC/ASUS service shutdown occurred.
- An independent observer saw the server alive, terminated its PowerShell supervisor
  during `-ObserveSeconds 10`, and observed server exit within three seconds plus
  REGDB_E_CLASSNOTREG afterwards. That observer returned no numeric server exit
  code; only process disappearance and class cleanup are established by this check.
- Own COM identity/reference checks passed 100 cycles including all added interface
  views; ordinary in-process SDK discovery with the private IPC receiver still passed.
  SDK client reference counters of 1/1/1 in external mode are its unused local roots,
  not evidence that the SDK's remote reference ownership problem is resolved.
- LightingService and ArmouryCrateService retained PIDs 6840 and 6608, both running
  as LocalSystem. PulseDeck stayed connected and acknowledgements advanced. Live
  service activation, Armoury Crate discovery, color reception and physical color
  matching remain unverified. No installed-agent or startup-task changes.

## Aura SYSTEM activation and temporary category publication — 2026-09-17

- Baseline client verified IsSystem=true/session=0 and failed activation with
  REGDB_E_CLASSNOTREG. The interactive native server had zero activations.
- Temporary x86 CLSID/AppID mapping with RunAs=Interactive User enabled the same
  SYSTEM client to enumerate one device and read its capabilities (S_OK). Neither
  LocalServer32 nor global DCOM access/launch permissions were changed.
- SYSTEM SDK test also passed three enumerations with full metadata assertions.
  Server totals matched the direct plus SDK clients: two activations, seven array
  enumerations, four capability reads, zero effect/synchronization requests.
- Temporary third-party Aura category entry then published for 30 seconds. All
  13 capability observations had ContainsProbeName=false and service PID 6840.
  No additional server activations were observed. This is a negative passive
  discovery result, not proof of a signature rejection or inability to load it.
- Every elevated run removed its unique scheduled task. Registration runs removed
  only newly-created probe keys; latest supervisor also verified all three exact
  class/AppID/category paths absent. It refuses pre-existing keys. Private results
  are stored outside Git; the SYSTEM phase receives the caller's runtime directory.
- User reports no scan button in Armoury Crate and apparent scanning on entering
  Aura Sync. A second, announced 60-second window produced 26 negative capability
  observations and no additional server activations. User confirmed the same devices
  remained visible after entering the page; this does not prove a HAL rescan occurred.
- Added a bounded read of EumerateHalInfo against the real registry, without calling
  CreateHal on any returned entry. During a subsequent three-second publication,
  SDK reported 17 registrations with exactly one probe GUID. Both service capability
  reads remained negative and server activation totals were unchanged. This verifies
  real registry visibility, not acceptance or refresh by the running LightingService.
- The final ordinary COM/SDK regression with forced server termination passed and
  class absence was confirmed. Zero probe processes/temporary tasks remained;
  configurator HTTP 200 and display connected with advancing acknowledgements.
- Installed agent, PulseDeck startup task and ASUS services were not replaced,
  stopped or restarted. No SwitchMode, Apply, LED setter or refresh call was made.

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

## Aura on-demand installation and removal — 2026-09-17

- Built a separate x86 GUI-subsystem HAL executable with GCC warnings as errors.
  It loads no ASUS SDK, calls no RGB control methods and remains separate from the
  installed agent. Existing 100-cycle COM ownership tests and temporary external
  COM/SDK plus forced-exit cleanup regression passed before persistent installation.
- The machine x86 LocalServer32 registration now launches the HAL automatically
  under the caller's identity. A SYSTEM/session-0 client with no pre-existing HAL
  process passed direct metadata reads and three SDK enumerations. The server
  recorded two activations, seven enumerations, four capability reads and zero
  effect/sync callbacks, then exited idle with references 1/1/1. This establishes
  on-demand activation from SYSTEM, not an actual pre-login or reboot observation.
- Initial installer checks exposed a PowerShell variable-name collision and then
  the x86 SYSTEM log's SysWOW64 redirection. Both handled failures removed their
  installation. Corrected workflow passed, then passed again after full removal.
- Existing-install refusal was verified. Removal deleted only owned category/class
  keys and known package files; native CoGetClassObject returned REGDB_E_CLASSNOTREG.
  Reinstallation passed the SYSTEM checks before republishing the category. Latest
  successful test server PID 35032, report 2026-09-17T13:04:30Z, state idle-exit.
  Audit is stored outside Git under the installer's LOCALAPPDATA.
- Final SDK registration inventory: 17 HAL entries, one probe GUID. The installation
  remains in C:\Program Files\PulseDeck Aura Probe for a later user-initiated reboot.
  Directory owner is BUILTIN\Administrators; SYSTEM/admin FullControl, Users
  ReadAndExecute. Only our two executables and two scripts plus manifest are present.
  No permanent task/service or AppID/RunAs mapping was created; no global COM ACL edits.
- Roundtrip left zero temporary probe tasks. LightingService PID 6840 and
  ArmouryCrateService PID 6608 remained running. A passive service-capabilities read
  after reinstall still returned ContainsProbeName=false. No additional host remained
  running. Installed PulseDeck agent, startup task and configuration were unchanged.
- No PC restart, service restart, SwitchMode, Apply, outgoing LED setter or manual
  refresh was used. Receiving colors is not implemented by the installed host;
  effect callbacks return E_NOTIMPL. Actual ASUS activation/tile discovery after
  reboot, continuous callback behavior, RGB correspondence and abrupt installer
  interruption remain unverified. Read diagnostic Status before any new self-test
  after reboot so our own activations cannot be mistaken for ASUS discovery.
- Configurator returned HTTP 200. A subsequent unrelated USB timeout entered
  `recovering` at acknowledgement 11587; the existing bounded recovery returned to
  connected with recovery count 13, acknowledgement 11678 and attempt budget reset
  to zero. No manual reconnect was issued. This is transport evidence; no new
  physical-image or RGB confirmation was requested during the Aura installation.

## Aura user reboot observation — 2026-09-17

- User reports no PulseDeck entry after the requested manual reboot and Aura Sync
  check. Windows LastBootUpTime is 2026-09-17T13:21:59.5000000Z. Both installed
  category/class registrations remain present. No install, activation self-test,
  registry change or refresh was run before collecting the existing report.
- Elevated read-only Status found the installed HAL running as PID 9916/session 0,
  created about 13:22:10 UTC. Its latest report at 13:22:26Z records one activation,
  two enumeration calls, one capability read, two effect requests, zero sync calls
  and HAL/device/factory references 5/5/2. The previous self-test baseline was PID
  35032 at 13:04:30Z, idle-exit, with zero effect calls. These are new startup calls,
  not counts carried over from our earlier tests.
- This is consistent with the ASUS stack loading the registered HAL at startup.
  The current report does not capture caller PID or distinguish effect callback
  variants/payloads, so exact caller attribution and RGB values are not established.
  Installed callback implementations still return E_NOTIMPL; two requests do not
  demonstrate accepted effects, valid colors or continuous updates.
- A subsequent read-only service capability query returned ContainsProbeName=false
  from LightingService PID 6456. ArmouryCrateService PID 6408 is running. The bounded
  event-log query found no matching probe DCOM events or probe/LightingService
  application crash events since boot; this is not a comprehensive error-log audit.
- PulseDeck display API reports connected, 163 acknowledged frames and zero
  recoveries since startup. No physical screen-off observation during the reboot
  was supplied, so Windows shutdown ordering/power-off remains unconfirmed.
- Runtime audit lives in LOCALAPPDATA/PulseDeck/aura-investigation/after-reboot.json,
  outside Git. No ASUS services, RGB settings, installed binaries or tasks changed.
  Next investigation is passive callback diagnostics/handling and device metadata,
  rather than repeating unchanged registration/reboot attempts.

## Aura Sync selection screenshot — 2026-09-17

- User explains that a discovered device must be selected in Aura Sync to join
  synchronization. Inspected the supplied screenshot directly: seven device tiles,
  all checked, and no PulseDeck tile. The visible search button belongs to the Hue
  connection section, not general HAL discovery. No UI actions were performed.
- Initial effect callbacks do not prove participation in synchronized effects;
  they may be initialization calls. Our current Static descriptor advertises
  synchronizable=0. Its influence on tile eligibility is an investigation target,
  not an established cause. Do not infer color reception or fix the flag blindly.
- Next acceptance sequence: tile appears, user selects PulseDeck, incoming callbacks
  and physical color correspondence are verified. Existing device selections and
  the current effect must be preserved. The screenshot remains outside Git.

## Aura detailed inventory and generic device metadata — 2026-09-17

- Added a separate bounded, read-only query for IServiceMediator.get_QueryAllDevice
  (DISPID 63, no parameters, BSTR result in the installed type library). The original
  get_QueryAllDeviceCap remains the default; both calls returned valid XML and left
  LightingService PID 6456 unchanged. No refresh or profile/control setter is used.
- Detailed inventory has 15 entries and already includes our old prototype:
  manufacturer PulseDeck, model Isolated test destination, Type=All,
  LightingName=All, one LED, index 0. This proves the service retained the device;
  prior ContainsProbeName=false only proved the literal name was absent. Aggregate
  capability XML reports groups/LED locations, not a complete per-device identity list.
- The metadata reader now identifies the probe by manufacturer/model and reports
  its classification separately. Query Devices returned ProbeIdentityCount=1 despite
  ContainsProbeName=false. Full vendor inventory remains outside Git.
- Static inspection of installed LightingService 3.10.12 associates type 0x64000
  (409600) with EXTERNAL_GENERAL in its device-type table. Service code also reads
  effects' Synchronized property, but no causal link to tile eligibility was proved.
  Updated our descriptor from type 0 to 409600 and model to PulseDeck Virtual Probe.
  Static's synchronizable=0 remains unchanged. Generic external classification is
  a test candidate, not proof Armoury Crate supports a corresponding tile.
- Added last incoming callback method/effect/count/VARTYPE to the bounded latest
  diagnostic report; no unknown buffer decoding, outgoing effect call or color
  claim. Methods still return E_NOTIMPL in the installed host. The old host had
  reached four incoming effect requests before replacement; payloads were not saved.
- Native build passed with warnings as errors. 100-cycle own COM identity/reference
  checks and three isolated SDK enumerations passed, now asserting device Type is
  exactly 409600 while keeping existing Static metadata assertions.
- Backed up installed own package and pre-update Status under private LOCALAPPDATA.
  Removed our registration, signalled only our host, waited for exit, then installed
  version 0.3.0-probe. SYSTEM automatic launch/direct metadata/three SDK enumerations
  and idle-exit checks passed; new report PID 8256 at 13:48:47Z, deviceType=409600,
  two activations/seven enumerations/four capability reads, no effect calls and
  references 1/1/1. Category published after tests; real inventory still has 17 HAL
  registrations with one probe GUID. Normal update succeeded; failure restoration
  branch was prepared but not exercised.
- ASUS services retained PIDs 6456/6408. Their detailed inventory after replacement
  still contains the cached old All classification/model, so no new tile result can
  be inferred from this installation. No service/PC restart was performed. Next
  normal user-triggered restart/scan must verify new service classification, tile
  appearance, user selection and then incoming request details.
- PulseDeck stayed connected: 1574 acknowledged frames, zero USB recoveries since
  startup at the final display check. Agent binaries, configuration and startup task
  unchanged; no physical RGB correspondence verified. Decompilation, package backup
  and runtime logs remain outside Git.

## Aura refreshed metadata and incoming envelope — 2026-09-17

- User still sees no tile. The current detailed service inventory now returns one
  EXTERNAL_GENERAL/PulseDeck Virtual Probe identity, so the updated metadata was
  acquired. Windows boot remains 13:21:59Z and LightingService remains PID 6456:
  no additional reboot or service restart was observed. SDK vendor logs at local
  15:53 identify PID 6456 enumerating our HAL; exact service attribution is now
  supported rather than inferred from timing alone.
- Native report for PID 44604 has eight incoming effect requests. Last call is
  method 3 (SetEffect2), effect 0, count 1, VARTYPE 8211 (VT_ARRAY|VT_UI4).
  LightingService logs report Apply Failed specifically for PulseDeck Virtual Probe.
  These are ASUS calls delivering to our destination; our tools did not invoke
  Apply, SetMode, SetLightColor or another outgoing RGB setter.
- Static inspection found EXTERNAL_GENERAL mapping in the Armoury Crate Aura plugin
  as well. This does not establish the remaining tile eligibility rules. No vendor
  binary, UI configuration, sync selection or effect setting was changed.
- Implemented a passive SetEffect2 effect-0 sink for the observed one-word envelope.
  Decoder validates variant flags/type, array rank/element width/type, bounds and
  count, copies only the single word and leaves input ownership with COM's caller.
  Wrong envelopes fail; other effect IDs/variants remain unsupported. Successful
  storage returns S_OK, with rawSamples/rawWord/rawSampleTick in the latest report
  and colorVerified=false. No agent provider, byte-order assumption or RGB rendering.
- Fifteen synthetic decoder checks passed on Windows, including nulls, BYREF, wrong
  count, empty/multiple elements, multidimensional and mismatched element arrays,
  plus valid nonzero lower bound. Tests call the decoder only, not HAL/SDK setters.
  Native warnings-as-errors build, 100-cycle COM ownership test and three isolated
  SDK metadata iterations also passed, with no effect calls in metadata tests.
- First UAC prompt was declined accidentally; user explicitly requested it again.
  Resent elevation succeeded. Backed up prior own installation/status, stopped and
  waited for only our host, installed 0.4.0-probe, and passed SYSTEM autoactivation,
  direct/SDK metadata and idle-exit checks. Baseline PID 17784, 14:08:34Z, references
  1/1/1, rawSamples=0, colorVerified=false. Services retained PIDs 6456/6408. Live
  receipt by this replacement still requires natural service activation; the test
  baseline is not a real RGB sample.
- Separately found USB delivery exhausted after needReSend:1|renderCnt:0: two
  recoveries, 27 acknowledgements, userDisconnected=false. No vendor TURZX process
  was listed. An initial reconnect request without the required local-client header
  returned HTTP 403 and made no change. One correctly formed explicit reconnect
  succeeded; later 108 acknowledgements and zero recoveries confirmed resumed
  transport. No physical artifact/power-off confirmation was inferred.
- After 0.4.0 installation, user left/re-entered Aura Sync and again reported no
  tile. No probe process was observed afterwards, and the latest probe-specific
  service delivery log lines were still from the prior 15:53 activation. There is
  no evidence this page visit activated the replacement. Therefore live S_OK
  delivery and any effect on UI eligibility remain unverified; another unchanged
  page-entry test was not requested. Installed binary hash matches the tested build.
- Final USB status: connected, 441 acknowledged frames, one subsequent bounded
  recovery. No further explicit reconnect was issued. This is transport evidence,
  not a fresh physical-image confirmation.

## Aura experiment retired at user request — 2026-09-17

- User asked to leave the virtual-device approach aside because of its complexity
  and missing Armoury Crate tile. Stopped investigation rather than proposing more
  category changes, reboots or RGB tests.
- Saved the own installed package and pre-removal Status to a private runtime backup,
  then ran its remover elevated. Verified category/class keys absent, native class
  check REGDB_E_CLASSNOTREG (0x80040154), installation directory absent and no
  PulseDeck.AuraHal process remaining. Source and previous evidence were preserved.
- LightingService and ArmouryCrateService remained Running with the same PIDs
  6456 and 6408 before/after. No service restart, vendor configuration change, sync
  selection or RGB control call. A vendor's cached device inventory was not forced
  to refresh during cleanup.
- PulseDeck remained connected, 538 acknowledgements and one recovery at final check.
  Agent, settings and startup task unchanged. Manual theme color remains available;
  real Aura RGB reception and selectable tile were not achieved. Runtime cleanup
  audit and backup remain outside Git. Do not restart this experiment without a new
  user request.

## Spotify artwork, compact widgets and background trial — 2026-09-17 (0.2.0)

- Started from clean `213971f`, installed 0.1.1 connected on identified COM5,
  elevated interactive agent, legacy TURZX task disabled and no vendor display app.
  Aura investigation remained retired; no ASUS changes or probes.
- Added compact 4×4 layout (16 bindings), retaining the classic eight-slot layout.
  Exact old bindings migrate without losing ID/name, label, maximum or visibility;
  eight new slots start hidden. Presentation adds numeric values, bars and rings.
  RAM uses physical `/ram` used+available, excluding `/vram`; total is usable RAM,
  not an invented capacity or necessarily the modules' marketed capacity.
- Installed configuration preserves original eight sources, adds rings for CPU/GPU
  usage and physical RAM used/free/total, and adds download/upload from the actual
  Ethernet interface. Filter-driver duplicates are not summed. Other six extra
  slots remain hidden. Existing accent, profiles, Discord and startup are preserved.
- Spotify thumbnail was obtained on Windows and visibly rendered in the shared
  preview beside matching title/artist. Playback and pause were observed. No Spotify
  credentials or HTTP artwork downloads were introduced. Missing/corrupt artwork,
  refresh failure, source/track invalidation and stale-art refusal are automated
  checks; physical matching and a controlled real track/cover change remain pending.
- 51 Core tests and 7 rendering tests passed; Angular production build (Node 24.15.0)
  and .NET 10 self-contained Windows publish passed. Full-frame/shutdown protocol
  bytes and recovery tests unchanged. New tests cover old configuration expansion,
  invalid sets/styles, physical vs virtual RAM, adaptive network units, artwork size
  limits/cache reset, last-slot output, stale artwork, GIF loop/disable, invalid
  backgrounds and animation suspension without overriding manual disconnection.
- Browser checked at 1500×1000 and 390×844: sixteen/eight slots switch correctly,
  extra-slot RAM/ring editing enables Save, Discard restores saved values, no page
  overflow or browser console errors. Final installed footer reports 0.2.0; animated
  background selector is off. Browser edits were discarded; intended configuration
  was saved separately through the validated local API.
- User GIF was 1280×1571, 35 frames, 3.2 seconds. Only the upper flight scene was
  cropped (1280×320 at y=180), scaled to 960×240 and resampled to 10 fps, 32 frames.
  Renderer targets 2 fps by selecting current animation time. Original and cropped
  assets remain outside Git; the latter is under LOCALAPPDATA/PulseDeck/assets.
  Cached animation decodes successfully; synthetic tests verify looping and stop.
- First Windows trial: static background acknowledged 16 frames in 20.54 seconds
  with one recovery (3.38 CPU seconds, about 216 MiB working set). Animated trial
  acknowledged only 2 frames in 40.91 seconds, with one more recovery and eventual
  timeout exhaustion (7.30 CPU seconds, about 226 MiB). These are failed-throughput
  observations, not a claim of stable 2-fps hardware output. Static mode also had a
  timeout, so animation cannot be identified as the sole cause.
- Disabled animation and explicitly reconnected after exhaustion. Static output
  recovered once and then exceeded 60 consecutive confirmed frames. Added
  `AnimationGuard`: a real transport recovery/error suspends animation on frame zero,
  advertises suspension in preview/UI and requires explicit toggle or asset change
  before retrying. The guard's transitions were tested with synthetic statuses;
  the final guard build has not been subjected to another animated USB stress trial.
- Both deployments backed up app/config/DPAPI credentials/task XML outside Git,
  stopped the agent, waited for process and hidden launcher exit, then replaced
  binaries and reused the existing elevated interactive task. Config/credential
  hashes matched across each binary deployment; intended layout settings were a
  separate save. Normal stop logged screen-off, standby COM3 appeared, startup woke
  to COM5 and acknowledged output. Final agent is hidden in user session 2.
- Final installed guard build: agent/Core DLL hashes match the tested publish,
  118 acknowledged frames with zero transport errors/recoveries at last check.
  Compact layout has ten visible bindings out of sixteen, animation off, real Spotify
  artwork available, physical usable RAM about 63.64 GiB. This is a short runtime
  observation, not proof the previously observed USB fault is resolved.
- New physical layout readability/artifact check, controlled Spotify cover changes,
  stable animated USB throughput and long-duration reliability remain unverified.
  Background animation is left disabled with the user's cropped first frame shown.
  No PC shutdown/reboot, firmware change, TURZX launch or RGB-control action.

### User physical confirmation after 0.2.0 deployment

- Asked to check artwork, CPU/GPU/RAM rings, network, image artifacts and Spotify
  track/artwork changes, the user reported everything working except the stationary
  GIF. This supplies user confirmation for those physical layout/media checks.
- The GIF was deliberately left static after the failed animated transport trial;
  this observation does not indicate a decoding failure. Animation remains disabled.
  Stable animated USB delivery and long-duration reliability are still outstanding.

## Static background and foreground executable icon — 2026-09-17 (0.2.1)

- User explicitly abandoned GIF animation and chose a dark, discreet static theme.
  Added the built-in dark gradient and removed animation UI/configuration, frame
  timing/cache and animation guard. Legacy unknown animation flags are ignored
  without resetting other preferences; legacy GIF files decode frame zero only.
  User assets/backups remain untouched outside Git. Rendering returns to about 1 Hz.
- Added Windows foreground PID/process/file-description/icon lookup. Icons are
  extracted with System.Drawing.Common 10.0.12, cached in memory with a 32-entry
  limit and refreshed after 60 seconds (10 for missing icons). Process handles,
  source icons and intermediate bitmaps are disposed. No executable path or window
  title goes into API state; no game injection, launcher scan or NVIDIA setting write.
  A restricted/unreadable process retains its name where possible, with no old icon.
- Game badge shares exact configured-process matching with ProfileSelector. Manual
  Gaming or profile debounce cannot incorrectly identify the foreground browser as
  a game. Actual app identity follows Alt-Tab independently of profile debounce.
- 51 Core and 8 rendering tests passed; production Angular build and self-contained
  Windows publish succeeded. Coverage includes retired-flag compatibility, exact
  game classification, current/stale icon matching and legacy static GIF handling.
  Existing full-frame and recovery tests remain unchanged.
- Clean starting Git state `cd0883a`. Before deployment 0.2.0 was connected with
  712 acknowledged frames and zero recoveries at the initial check. New app copied
  only after the agent and hidden launcher exited, with app/config/DPAPI credentials
  and task XML backed up. Config/credential hashes matched across binary deployment;
  separately saved only an empty background path to choose the requested built-in
  theme (the obsolete animation flag is omitted on save). Other preferences retained.
- Final 0.2.1 runs hidden/elevated in interactive session 2, one agent, existing
  PulseDeck task Running and legacy TURZX task Disabled. Shutdown logged screen-off,
  standby COM3 was observed, startup woke identified COM5 and acknowledged frames.
- Windows API returned Windows Terminal Host with available icon. The actual shared
  preview then showed Google Chrome and its icon after a foreground transition;
  subsequent API observation showed WhatsApp.Root with a different icon hash.
  Dark background, existing ten widgets, Spotify artwork and Discord remained visible.
  No game was launched for this check: actual game icon and physical readability
  still require user confirmation, while classification is covered by tests.
- Browser settings checked at desktop and 390px: background field empty, animation
  selector absent, existing game list retained, no horizontal overflow or console
  errors. Widget editor still has sixteen slots and ten visible bindings.
- One pre-existing-style USB timeout recovered automatically in static mode: 62
  frames confirmed with one recovery at that observation. This is not a claim the
  earlier USB reliability issue is solved; no animation test was performed.
  Final check: 136 frames, one recovery, connected; installed agent/Core DLL hashes
  match the tested publish, current application icon available.
- Read-only `nvidia-smi` successfully queried RTX 5070 Ti/driver 610.88 plus utilization,
  temperature, VRAM and power. No new NVIDIA provider was installed. Official NVAPI
  DRS documentation describes driver profiles/applications, not proof of locally
  installed games; no documented public NVIDIA App library API was found in this
  investigation. Sources and future launcher-based discovery are in the roadmap.

### User physical confirmation after 0.2.1 deployment

- User confirmed the dark background and foreground application icon look correct
  on the physical display. A game was explicitly not tested; its actual icon and
  game badge remain pending a real foreground-game check.

## Gaming presentation and tracked Discord header — 2026-09-17 (0.2.2)

- Started from clean `84e2f1a`, installed 0.2.1 connected, one hidden interactive
  agent, elevated PulseDeck task Running and legacy TURZX startup Disabled. The
  configured tracked Discord user was present with mute/deaf false. No vendor
  display process was running. Existing ten widget bindings, manual accent,
  game-process list and empty general background were preserved during development.
- Added a Gaming composition with separate game/GPU area, the same sixteen sensor
  slots and media/Discord area. NVIDIA is a text badge derived from the real GPU
  hardware type, not an installed-game source or copied vendor logo. FPS remains
  unavailable. A setting can retain the original compact/classic layout in Gaming.
- Added per-process local static-image associations, editable in a dedicated Angular
  settings component. No automatic artwork downloads or bundled game assets. The
  first real game image has not been supplied; the configured fallback remains dark.
  Case/extension-equivalent duplicate mappings and malformed entries are rejected.
- Game presentation is separate from actual foreground identity: automatic profile
  entry/exit retains the configured delay, brief Alt-Tab retains game artwork, direct
  game changes replace it, and manual Gaming without a recognized game makes no game
  detection claim. Missing/deleted images do not retain another game's background.
- Header shows only the configured member's current nickname and MIC ATTIVO/MUTE/
  DEAF. Empty configuration, absent member and disconnected source have explicit
  states. Stale tracked snapshots and other participants cannot supply its status;
  no speaking detection is implied. Long nicknames are constrained before the clock.
- 56 Core and 11 rendering tests passed; Angular production build and Windows
  self-contained publish passed. New checks cover legacy-config defaults, duplicate
  mapping validation, profile/scene transitions, roster selection/unavailability,
  gaming card sixteen, per-game background replacement/deletion and header updates.
  Full-frame bytes, USB recovery and shutdown code/tests remain unchanged.
- Inspected an isolated rendering preview using an actual state snapshot and a
  separate explicitly labelled synthetic game fixture. Fixtures stayed outside Git
  and were never sent to USB or represented as live game data. Adjusted RAM used/
  total font fitting in the narrower Gaming cards; Windows rendering and physical
  confirmation are separate checks.
- Both installation passes backed up app/config/DPAPI credentials/task XML, waited
  for the agent and launcher to exit, observed logged screen-off/standby COM3, then
  reused the existing hidden elevated interactive task to wake COM5. Binary-deploy
  config/credential hashes were unchanged. The second pass corrected an Angular
  output named `change` colliding with the native input event; it now uses
  `themesChange`. The malformed request was rejected without overwriting settings.
- Installed browser checks passed at 1500px and 390px: create association, reject
  missing file with HTTP 400 and explicit error, save empty-path fallback, reload
  persisted association, change its process and remove the row. Final normal UI
  interactions produced no console/page errors or overflow; sixteen widget slots
  remain available. Test settings were restored, and all prior config values compare
  equal; only the new default fields are added by the saves.
- Windows manual Gaming preview showed real GPU/sensor/media/Discord data and the
  new header; automatic mode was restored in finally. Actual foreground FC26 was
  not in the existing game list, so `game` remained null rather than being invented.
  Its executable metadata/icon was unavailable in the read attempted; no game
  injection or process modification. Adding FC26 was offered separately and has
  not been assumed. Real configured-game focus/artwork and physical readability,
  mute/deaf transitions and channel leave/rejoin remain unconfirmed.
- Final agent/Core DLL and frontend index hashes match the tested staged build.
  A subsequent USB `needReSend:1|renderCnt:0` exhausted the existing two recovery
  attempts after 52 acknowledgements, with `userDisconnected=false`. This is a
  recurrence of the unresolved transport issue, not evidence of a stable new layout
  or a diagnosed cause. An explicit reconnection was initiated to restore usability.
- Reconnection succeeded on the identified COM5 device. The follow-up observation
  reached 72 acknowledged frames, zero recoveries and no transport error since that
  reconnect. This is a short successful interval, not proof of long-term USB
  reliability. No new physical-image confirmation, PC shutdown/reboot, vendor app,
  firmware change, Aura probe or RGB-control action was performed.

### FC26 opt-in and automatic artwork investigation

- User explicitly approved adding FC26. Backed up the current configuration outside
  Git and appended only `FC26` through the validated local API; original bf6 and
  Battlefield entries and other preferences retained. No binary update/restart.
- User clarified that game imagery should be found automatically, rather than
  requiring manual image selection. Read the installed NVIDIA App's local application
  catalog and selected Windows uninstall metadata. Both sources identify installed
  FC26/Battlefield 6 executables. A direct Windows ExtractAssociatedIcon call against
  FC26's registered file succeeded with a 32×32 icon; its live-process MainModule
  lookup had been unavailable. This is isolated evidence for a future fallback,
  not an icon-provider fix already deployed.
- Confirmed Steam library image cache exists. Inspected only selected NVIDIA asset
  metadata and two images: the 456×253 PNG cache includes promotional/partial graphics;
  no trustworthy FC26 background association was established. NVIDIA `ImageFiles`
  entries examined are executable paths, not high-resolution cover-image paths.
  No vendor settings/assets were changed, no account/browser credentials inspected,
  no vendor binary executed and no private catalog or image committed.
- Display was connected at the configuration follow-up. Foreground then was Windows
  Terminal, so an actual FC26 automatic transition and its physical presentation
  still need observation after the game returns to the foreground.

## Twelve sensors and weather column — 2026-09-17 (0.2.3)

- User's final preference is twelve sensor widgets and a dedicated weather column
  on the left, with the city selectable in the app and Rome, Italy initially.
  Preserved the original ten bindings and enabled actual CPU Package power and
  GPU Core clock by ID plus name. The four remaining slots are hidden with their
  binding fields retained. All runtime configurations/backups remain outside Git.
- Added `weather` layout: three columns/four rows of sensors, left weather card and
  existing right Spotify/Discord region, also in Gaming. The standard sixteen-slot
  and classic eight-slot layouts remain available; all sixteen bindings are stored.
  Existing header/foreground/game-background behavior and manual accent retained.
- Weather is opt-in through that layout and a selected city. Open-Meteo geocoding
  returned Roma / Provincia di Roma / Lazio / Italia at 41.89193, 12.51133; this
  disambiguates several other Italian results named Roma. Actual forecast endpoint
  returned current conditions, daily minimum/maximum and Europe/Rome timezone.
  These are remote model estimates, identified in the panel; no physical weather
  sensor or geolocation of the user's PC is implied.
- Core weather feed starts one non-blocking HTTP request, caches success fifteen
  minutes and retries errors after two. Five-second cancellation/64 KiB bounds,
  timestamp/unit checks, city-change invalidation and clear-on-failure behavior.
  No weather request when another layout is selected; search only runs on user action.
- 61 Core and 12 renderer tests passed. Coverage includes coordinates/defaults,
  units/missing readings/model timestamps, request caching/retry, delayed response
  after city change, non-blocking access, oversized response/suspend expiry, twelve
  cards, inactive extra slots, unavailable readings and unchanged weather region
  across Gaming transitions. Angular production build and self-contained Windows
  publish passed. USB/full-frame/shutdown implementation unchanged.
- Inspected a standalone rendering using the real Rome response and a snapshot of
  the actual twelve sensors. No synthetic weather was sent to the physical panel.
  Physical readability and externally induced network-loss behavior remain distinct
  from these renderer/feed tests.
- The first deployment request was cancelled in Windows UAC; that attempt never
  started the elevated script and left 0.2.2 running with twelve active sensors.
  The user subsequently explicitly requested another prompt and accepted it.
- Installed 0.2.3 after backing up the app, configuration, credentials and startup
  task definitions outside Git. Agent and launcher fully exited before replacement.
  Normal stop logged screen-off; standby identity CT88INCH appeared on COM3 and
  automatic wake reconnected the identified 8.8-inch panel on COM5 with acknowledged
  frames. One health request timed out during startup; readiness subsequently passed.
  Agent/Core hashes matched the tested package. Configuration and credential hashes
  were unchanged by deployment. Agent runs without a main window in interactive
  session 2; PulseDeck task remains Highest/Interactive and TempMonitor_8 disabled.
- Used the installed configurator to search Roma, select the Lazio/Italy result and
  save it, then select/save the twelve-sensor weather layout; both PUTs returned 200.
  Reload preserved Rome and layout. Searching "Roma, Italia" also returned the
  correct disambiguated result. Switching to sixteen slots and discarding restored
  twelve. No page errors or horizontal overflow in checked settings/widgets at
  desktop 1500px and mobile 390px. Final config differs from the deployment backup
  only in layout/weatherLocation; all sensor bindings and other preferences match.
- Live weather reported Roma at 26 degrees Celsius, model time 19:30 Europe/Rome,
  and status connected. Inspected the actual Windows preview: weather, twelve
  sensors, tracked Discord header and media region fit without overlapping text.
  The active media session was WhatsApp at capture time; Spotify cover changes were
  not re-tested in this deployment. COM5 reached 144 acknowledged frames with no
  recovery/error at the final follow-up. These checks do not establish physical
  readability, indefinite USB stability or externally induced network-loss handling.
  No PC shutdown/reboot, firmware, Aura or vendor-app action occurred.
- User subsequently confirmed the physical 0.2.3 panel: Rome weather on the left
  and all twelve sensors are readable, with a correct image and no residual content
  or anomalous pixels. This closes the physical readability/artifact check for this
  layout; prolonged USB stability and externally induced weather network-loss checks
  remain separate outstanding validations.

## Selectable RSS/Atom news footer — 2026-09-17 (0.2.4)

- User requested news in the footer with selectable RSS sources, custom URLs and
  ready-made presets; explicitly chose ANSA top news, ANSA technology and Multiplayer
  PC as initially active sources. Existing weather/twelve-sensor setup is retained.
- Added a dedicated Angular news settings component with five verified presets,
  up to eight named channels, independent pause/remove, custom URL entry, global
  enable and 10–120-second rotation (20 by default). Article links remain available
  in the configurator; long display titles are shortened. Legacy settings disable
  news without making requests; all source choices require normal config save.
- Portable provider supports bounded RSS 2.0/Atom parsing, cancellation, cache/retry,
  invalidation on channel change/disable, markup removal, date/link checks and source
  interleaving. HTTP uses validated public addresses with no proxy, cookies or
  automatic redirects. Failed sources clear their own titles; others remain usable.
  No article bodies, remote images or credentials are requested/stored in Git.
- 67 Core and 15 renderer tests passed, including asynchronous obsolete-result
  handling, partial failure/expiry, rotation, XML external-entity rejection,
  oversized feeds, URL/private-address checks and footer-only pixel changes across
  weather/compact/classic layouts. Angular production build and self-contained
  Windows publish passed; USB command/recovery/shutdown code unchanged.
- All five official preset endpoints returned HTTP 200 directly from Windows;
  the real Core parser accepted ten dated items from each. A separate live process
  also exercised the production public-address HTTP handler and NewsFeed: the three
  selected sources all connected with ten items each. Inspected standalone preview
  using real feed data and actual state; article/feed assets remain outside Git.
- First UAC deployment request was cancelled before its script ran. The user
  explicitly approved resending it and accepted the second Windows request.
- Installed the package after backing up app/config/credentials/task definitions,
  waiting for the old agent and launcher to exit, and observing standby identity
  CT88INCH on COM3. Automatic wake restored the identified panel on COM5. One
  health probe timed out during startup before readiness succeeded. Binary hashes
  matched the staged build; settings and credential hashes survived deployment.
- Browser testing found that rapidly adding a preset then changing enable state
  could overwrite a pending input update. Changed the news editor to a local
  writable Angular model and rebuilt. Deployed that UI correction with another
  complete stop/backup/start, again preserving settings and hidden startup. The
  corrected UI loaded main-ZGAS46UA.js. Simultaneous preset additions and enable
  changes now preserve all five draft sources in the regression check.
- Installed UI checks passed for preset add/remove, global disable (runtime status
  disabled), custom HTTPS URL addition/save/reload, and restoring the three requested
  sources. All config PUTs returned 200. Final sources each report connected with
  ten items (thirty total). Article list provides publisher links with a separate
  tab and noopener; no horizontal overflow at 1500px or 390px. No page errors during
  the final interaction checks. Custom test configuration was removed afterward.
- Final configuration differs from the pre-news backup only in the added `news`
  settings. Weather location/layout, every sensor binding and all other preferences
  match. Agent remains hidden in interactive session 2, scheduled task is
  Highest/Interactive, and the legacy vendor task stays disabled.
- Inspected the actual Windows preview: source/title/date fit within the footer,
  with weather, twelve sensors, current Spotify cover and Discord header intact.
  Physical footer readability/rotation awaits user confirmation. Long-term USB
  stability and real network-outage recovery remain separate from unit/UI checks.
  No PC shutdown/reboot, firmware, Aura or vendor-app action occurred.
- Successive installed previews showed different headlines and source labels as
  snapshot time advanced, consistent with the twenty-second rotation. Spotify also
  changed track and cover during this observation. Before the size adjustment, COM5 reached
  176 acknowledged frames, zero recoveries and no transport error since the
  last restart; this is a short observation window, not a long-term USB guarantee.
- On seeing the footer, the user requested it slightly larger rather than approving
  its physical readability. Increased default title text from 20 to 24 px, enlarged
  source/date, and exposed 20/24/26 px choices in the app. Long headlines now keep
  the selected size and use ellipsis. Renderer regression checks confirm all three
  sizes leave every pixel above y=446 unchanged in weather/compact/classic layouts.
- Rebuilt and installed the size adjustment using complete stop, backup and hidden
  task restart again; agent/Core hashes and config/credential preservation checks
  passed. The final configurator loads main-SGDI2BHN.js. Saved 24 px from the UI
  (HTTP 200), reloaded and verified size plus the same three active sources. Inspected
  actual Windows preview with the larger gaming headline: it fits without touching
  widgets. COM5 reached 100 acknowledged frames, zero recoveries and no transport
  error since this last restart.
- User subsequently confirmed that the 24 px footer is sufficiently large and
  readable on the physical display. This closes the enlarged-text readability
  check; it is not an additional confirmation of rotation timing or long-term USB
  stability beyond the separate observations recorded above.

## Recurring display-only freeze — 2026-09-17 (0.2.5)

- User reported a freeze and clarified it affected only the physical PulseDeck
  display. Windows process and health API remained responsive. Display status was
  error with `needReSend:1|renderCnt:0`, two attempts/two recoveries, 129 acknowledged
  frames and userDisconnected=false. Log sequence shows full-frame recovery twice
  followed by another resend; ordinary acquisition/UI were not the stopped component.
- Saved diagnostics/config/state outside Git before one explicit reconnection.
  COM5 resumed, but the same pattern exhausted again at 116 acknowledgements after
  roughly two minutes. This reproduces the limitation without configuration changes
  or synthetic image/fault injection; manual reconnection is only temporary relief.
- Changed the second existing retry to close/reopen/reinitialize the identified
  awake device when a resend recurs after the first attempt, including after a
  successful full resend. The first request still tries a full frame on the open
  port. Reopen uses the established identity/ROM checks and resets the partial
  counter; it never invokes standby wake. No third attempt, loop or renewed budget
  after one successful recovery. Explicit disconnect/shutdown cancellation preserved.
- Added last attempted frame kind and partial counter to API/status-change logs to
  distinguish future failures. The origin of the first resend is not established.
  Reviewed the pinned upstream revision-C driver; its partial count increment alone
  does not establish that arbitrary counter resets on an open session are valid.
  The fix therefore reuses the already-tested connection initialization path.
- 70 Core and 15 renderer tests passed, plus Angular production build and Windows
  publish. New checks reproduce full success followed by another partial rejection,
  verify second-attempt reopen/counter restart and cancellation before that reopen.
  Persistent resends still exhaust exactly two retries. Verified full-frame command
  C8 EF 69 00 38 40 0E 10 and all encoding tests remain unchanged.
- Published 0.2.5 into a new deployment directory after the first supervisor found
  an old completion marker and left the installed version unchanged. For the actual
  deployment, one guarded connection restored the error-state port for normal
  shutdown; it would not override a user-disconnected state. Backed up the app,
  config, credentials and task definitions; old agent/launcher exited fully before
  replacement. Screen-off logged, standby COM3 observed, startup woke COM5 again.
  Agent/Core hashes matched the tested package and config/credential hashes were
  unchanged. One startup health probe timed out before readiness succeeded.
- Installed health/footer report 0.2.5; agent is hidden in interactive session 2,
  task remains Highest/Interactive and TempMonitor_8 disabled. Rome weather, twelve
  sensors, all three news channels and the approved 24 px footer remain configured.
- A real resend recurred on 0.2.5 at acknowledgement 137, partial counter 136.
  The first retry acknowledged full frame 138, but partial counter 137 was rejected
  again. The second retry reinitialized the port and acknowledged full frame 139;
  subsequent partial frames resumed with counter zero. The passive observation then
  reached acknowledgement 188 / partial counter 48 without another reconnect command.
  This directly exercises the new escalation on the real device, not a simulated
  transport error. The original first resend still occurs; its cause is unresolved.
- User explicitly confirmed after that automatic recovery that clock, sensors and
  news update again and the physical image is correct. Inspected the current Windows
  preview as well; weather, twelve sensors, Spotify and news remain intact. No PC
  shutdown/reboot, firmware, vendor-app or Aura action was performed.
- Completed a five-minute passive sampling window after startup: final state was
  connected with 338 acknowledged frames, partial counter 198, two recoveries from
  the single observed outage, attempt budget reset to zero, and weather/news still
  connected. After full recovery frame 139, 199 further partial frames succeeded
  without another manual connection or transport error. Budget reset was logged at
  frame 198 after sixty consecutive acknowledged frames. This verifies the bounded
  recovery improvement and short-term continuity, not elimination of the initial
  USB fault or a long-duration/cable/suspend reliability guarantee.

## Screenshot-directed readability adjustments — 2026-09-17 (0.2.6)

- Read the user's annotated screenshot from /tmp without copying it into Git.
  Red marks identify the small foreground-app caption and weather credit line;
  white marks identify foreground name, RAM used/total/free, media elapsed/duration,
  weather humidity/wind/model time and the news publication timestamp.
- Removed those two captions from the shared renderer. Open-Meteo attribution and
  the explanation that weather is a model estimate remain in the configurator/docs.
  Foreground name is now 28 px, media time 20 px, RAM used/total up to 26 px (22 px
  minimum fitting) and free memory 20 px. Humidity, wind and model time each use a
  separate 20 px line; news time is at least 20 px. Marked secondary readings now
  use the normal light text color for contrast. Existing twelve sensor bindings,
  news title size, header nickname and media cover remain unchanged.
- 70 Core and 15 renderer tests, Angular production build and Windows publish passed.
  Inspected standalone rendering against an actual state snapshot; all marked text
  fits. USB recovery/encoding/shutdown implementation is unchanged from 0.2.5.
- Before this update, the running 0.2.5 reported connected at 962 acknowledged
  frames, six recoveries, zero current attempts and no current error. The historical
  resend remains in diagnostics; this does not mean the initial USB fault is gone.
- Deployed 0.2.6 after the old agent and hidden launcher fully exited. Backup:
  `%LOCALAPPDATA%\PulseDeck\before-readability-026-20260917-204022`.
  Stop logged screen-off and standby COM3 was observed; startup rediscovered COM5.
  Configuration and encrypted credentials hashes are unchanged. The elevated
  interactive PulseDeck task is running with no window; legacy TURZX task stays disabled.
- Installed API and configurator report 0.2.6. Inspected the actual Windows PNG:
  removed captions are absent, enlarged RAM/weather/media/header/news-time text
  fits without overlap. At 79 acknowledged frames the display was connected,
  transmitting partial frames with zero recovery attempts/errors; weather and news
  were connected.
- The user subsequently confirmed on the physical display that all marked text is
  readable and both captions marked for removal are gone: “Sì, tutto corretto e
  leggibile”. This confirms the 0.2.6 readability changes on the panel.

## Remove secondary RAM and weather text — 2026-09-17 (0.2.7)

- After approving 0.2.6 readability, the user requested removing the free-memory
  line and the entire “Dati locali delle…” weather line. The shared renderer now
  shows only RAM used/total beneath its label, aligned with the other main ring
  values. Weather retains humidity and wind; model time remains in API state.
- 70 Core tests, 15 renderer tests, Angular production build and Windows publish
  passed. After the additional weather request, renderer tests and Windows publish
  were repeated successfully against the final source. No transport changes.
- Before deployment, 0.2.6 was connected on COM5 with 1,805 acknowledged frames,
  zero recoveries and no transport error in that session.
- Installed 0.2.7 with backup `before-readability-027-20260917-211129` outside
  Git. Old agent and launcher exited before replacement; screen-off/standby COM3
  and automatic wake to COM5 were observed. Configuration and credential hashes
  are unchanged; hidden elevated interactive startup is running, TURZX disabled.
- Windows preview inspected: free-memory and weather-time lines are absent; RAM
  used/total remains legible and aligned. Configurator and health report 0.2.7.
  At 21 acknowledged frames, partial transmission continued without errors or
  recoveries. Physical confirmation of these two removals is still pending.

## Electron configurator shell — 2026-09-17

- Added an independent Windows x64 desktop package (Electron 44.4.1, Packager
  20.3.0), reusing the running 0.2.7 frontend/API. Two URL-boundary tests passed;
  npm audit reported zero vulnerabilities. Packaged successfully from WSL.
- Installed under `%LOCALAPPDATA%\PulseDeck\desktop-app` with a Start menu
  shortcut. No agent DLL, scheduled task, display settings or credentials changed.
- Windows Electron loaded Angular with AGENT ONLINE, the live PNG preview and
  widget controls. CDP inspection confirmed `require` and `process` are undefined
  in the renderer. Screenshot inspected outside Git. Debugging was enabled only
  for this check; the normal shortcut has no debugging arguments.
- Launching again retained the same window PID (1864), with exactly one main
  window. Closing the shell left zero Electron processes while agent PID 58664
  continued on COM5 at 368 acknowledged frames without errors/recoveries. Reopened
  normally after the test. The agent was never restarted for this installation.
- Agent-absent scheduled-task startup and retry/error dialogs were not exercised
  against the live installation; the panel was kept running. External-link browser
  handoff, a fresh Windows login and user acceptance remain unverified. This package
  is unsigned and has no auto-updater. No additional USB/physical-panel claim.

## Gaming layout, sessions, artwork, FPS, voice and master volume — 2026-09-17 (0.3.0)

- Shared renderer now replaces the weather/left column with eight Discord rows
  (additional pages rotate every eight seconds), and media/right column with game
  artwork, full name, process-session duration and application FPS/frame time.
  Weather layout keeps the same twelve sensor positions. Speaking highlights only
  apply in the dedicated Gaming roster, never to the desktop roster/header.
- Windows process start time supplies session elapsed time. A signed-in Windows
  harness confirmed the same process/start time survives a read with no selected
  game and elapsed time continues. BF6 Alt-Tab/relaunch has not yet been observed.
- Local NVIDIA catalog resolved verified existing EXEs to Battlefield 6 and
  EA SPORTS FC 26. Windows artwork requests matched Steam apps 2807960 and 3405690
  respectively and downloaded their actual covers into `%LOCALAPPDATA%\PulseDeck\game-artwork`.
  A Windows preview using those resolved identities/covers and a live hardware
  snapshot was inspected; its game selection was a fixture, not proof of a live game.
- Windows master-output volume read 62%, unmuted. Actual deployed preview shows
  the volume beside the clock and the shorter nickname/status area without overlap.
  This implementation only reads volume/mute and follows the default multimedia
  endpoint. Manual volume/mute and output-device transitions remain unverified.
- PresentMon 2.5.1 console is packaged with pinned SHA-256, filtered by selected PID,
  private trace-session name and bounded two-second sample window. Tests reject
  other PIDs, malformed/NaN/zero frame times, separate swap chains and clear stale
  data. Binary execution/help verified on Windows. No BF6/FC26 FPS samples have
  yet been observed, so in-game compatibility and sampling overhead are unverified.
- Discord.Net 3.20.1 Voice integration forces DAVE, self-mutes the bot, consumes
  SpeakingUpdated, and discards input streams without storage/playback. It joins
  only with dedicated Gaming plus the enabled option, disconnects outside Gaming,
  and retries failures at most every 30 seconds. libdave/Opus/libsodium loaded in
  a signed-in Windows harness; this is not proof of a successful Voice handshake
  or speaking transitions. Those checks remain pending with a game/call active.
- 72 Core tests and 17 actual-renderer tests passed, including Gaming side-column
  replacement, fixed sensor positions, eight-member roster, speaking isolation and
  stale-highlight clearing, and volume changes confined to its header region.
  Angular 22 production build and self-contained win-x64 publish passed. Both
  Python and PowerShell dependency packaging downloaded and checked the pinned
  binaries successfully. The normal transport/protocol implementation is unchanged.
- Updated the running installation only after old agent and launcher/task exited.
  Backup: `%LOCALAPPDATA%\PulseDeck\before-gaming-030-20260917-223459`.
  Runtime configuration and encrypted credential hashes remained unchanged. Reused
  the existing elevated interactive-user task without modifying it. Agent 0.3.0
  reconnected to COM5, identity `chs_88inch.dev1_rom1.90`; at 37 acknowledged frames
  it had zero recoveries and no reported error. Actual Windows PNG inspected with
  music/weather/Discord connected; Voice inactive outside Gaming. Physical layout
  acceptance, FPS during BF6 and voice transitions remain pending.

Sources: [PresentMon console](https://github.com/GameTechDev/PresentMon/blob/v2.5.1/README-ConsoleApplication.md),
[Discord.Net audio events](https://docs.discordnet.dev/api/Discord.Audio.IAudioClient.html),
[DAVE setup](https://docs.discordnet.dev/guides/voice/libdave.html),
[Windows endpoint volume](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume).
