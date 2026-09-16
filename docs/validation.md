# Validation — 2026-09-16

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
