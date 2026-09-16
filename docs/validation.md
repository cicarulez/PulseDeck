# Validation — updated 2026-09-17

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
