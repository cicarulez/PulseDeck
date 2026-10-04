# Display refresh diagnostic

Runs in the signed-in Windows session against the **already connected** verified
`chs_88inch.dev1_rom1.90` / `0525:A4A7` panel. Stops if TURZX is running or identity
changes. Temporarily disconnects the agent's serial writer, reads its live PNG
preview, and transmits those images directly; the agent and data collection stay
running. Reconnects the agent in `finally`. Does not flash firmware or change config.

Build:

```sh
dotnet publish tools/display-refresh-probe/PulseDeck.DisplayRefreshProbe.csproj -c Release -r win-x64 --self-contained true -o artifacts/display-refresh-probe
```

Windows commands:

```powershell
# One command for each changed region, 90 seconds:
.\PulseDeck.DisplayRefreshProbe.exe --send
# Same regions with a 20 ms delay after each acknowledgement:
.\PulseDeck.DisplayRefreshProbe.exe --send --paced
# All changed regions in one addressed partial command, 90 seconds:
.\PulseDeck.DisplayRefreshProbe.exe --send --batch
# Single global bounding rectangle, five minutes:
.\PulseDeck.DisplayRefreshProbe.exe --send --single
# Full frames with a 4 Hz maximum, five minutes:
.\PulseDeck.DisplayRefreshProbe.exe --send --full4
# Full frames requesting up to 8 Hz, five minutes; reports encode/write/ACK timing:
.\PulseDeck.DisplayRefreshProbe.exe --send --full8
# Same trial: normal process priority for 150 s, then AboveNormal for 150 s.
# Only the standalone probe's CPU scheduling priority changes; no USB priority
# or persistent Windows setting is changed. Restores its original priority.
.\PulseDeck.DisplayRefreshProbe.exe --send --full8-priority
# Batched regions at an 8 Hz maximum, three minutes:
.\PulseDeck.DisplayRefreshProbe.exe --send --batch8
# Five-minute command stress at up to 10 Hz: flush and wait 20 ms before querying.
# Repeats current spectrum pixels between fresh previews; not a visual 10 fps test.
.\PulseDeck.DisplayRefreshProbe.exe --send --settled
# Replay the last rejected payload five times after fresh full baselines:
.\PulseDeck.DisplayRefreshProbe.exe --send --replay
# Diagnostic deliberately holding the partial counter at zero (expected rejection):
.\PulseDeck.DisplayRefreshProbe.exe --send --batch0
# 100 repetitions at each height 1, 2, 3, 4, 8 and 16, with a 20 ms delay:
.\PulseDeck.DisplayRefreshProbe.exe --send --thin
```

Uses the existing panel picture for thin-region checks, not invented readings.
Prints timing, regions, encoded/raw byte counts, host counter and device status.
On a rejected live update, saves the rejected encoded region payload and header/report under
`%LOCALAPPDATA%\PulseDeck\display-refresh-probe`, outside the repository. That may
contain pixels from the user's live display; do not publish it without inspection.
The full-frame command remains unchanged. API/USB acknowledgement is not proof of
physical appearance: check for artifacts and confirm that the display follows the
current song/effect. Preview fetching and decoding add overhead to the benchmark.
