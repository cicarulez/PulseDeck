# Physical notification animation probe

Windows-only, explicit one-shot experiment. Keep PulseDeck running and connected
to the verified `chs_88inch.dev1_rom1.90` device (`0525:A4A7`).

```sh
dotnet publish tools/notification-display-probe -c Release -r win-x64 --self-contained true -o artifacts/notification-display-probe
```

Copy the published tool to a runtime folder outside the repository and run its
supervisor in the signed-in Windows session:

```powershell
.\Invoke-Probe.ps1 -ProbeDirectory "$env:LOCALAPPDATA\PulseDeck\notification-display-probe"
```

The supervisor supplies `--send`, caps the child process at one minute, writes
only probe diagnostics into that runtime folder and reconnects in `finally`.
The executable without `--send` performs no operations. The probe:

- Validates the current connected display, USB identity and firmware response.
- Holds the current preview in memory, with no screenshot persisted.
- Disconnects only the agent's serial port; all providers keep running.
- Displays a countdown and three trials with the synthetic mail count of three.
- Uses the existing full-frame protocol and waits for each acknowledgement.
  Animation uses wall time, skips missed frames and never queues transfers.
- Reports acknowledged frames and transfer timing, then reconnects the agent
  in `finally`, including ordinary error paths. No firmware, configuration,
  scheduled tasks, credentials or published agent binaries are modified.

The static background is a temporary snapshot, not live sensor data. The badge
is not a real mailbox count. Acknowledgements do not prove visual correctness;
ask the person at the panel to judge smoothness, flicker and placement.

Allow about 25 seconds. Use a supervising process with a bounded timeout and a
final reconnect for abnormal termination. If interrupted outside normal cleanup,
use Connect in the configurator. A transport error aborts this experiment rather
than repeating serial commands indefinitely.
