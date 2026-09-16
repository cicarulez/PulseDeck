# TURZX 8.8-inch communication probe

This Windows-only diagnostic renders a 1920×480 test screen, then updates the
248×163 Discord placeholder at the bottom left. All displayed names and bars are
**demo data**. It does not integrate live Discord or hardware sensors yet.

The connected unit was enumerated as `0525:A4A7` on `COM5`. The probe checks both
the USB identifier and the device's response before transmitting graphics.
Close TURZX, including its tray process, and leave the display powered and
connected. With `--send`, the current video is stopped and the test replaces the
display contents. The probe releases the COM port on completion. To restore the
original display, reopen TURZX and start Earth Theme.

## Setup (PowerShell)

Use a separate environment and an external checkout of
[turing-smart-screen-python](https://github.com/mathoudebine/turing-smart-screen-python).
The driver retains its upstream GPL-3.0-or-later license and is not vendored here.

```powershell
$probeDir = Join-Path $env:TEMP 'discord-overlay-turzx-probe'
python -m venv "$probeDir\venv"
& "$probeDir\venv\Scripts\python.exe" -m pip install pyserial==3.5 Pillow==12.3.0 numpy==2.4.6
git clone https://github.com/mathoudebine/turing-smart-screen-python.git "$probeDir\driver"
git -C "$probeDir\driver" checkout 2b33ab4f00a096916dd6a1174441a53a7ec33b03
```

From the PulseDeck repository root, generate the test images without
opening the display:

```powershell
& "$probeDir\venv\Scripts\python.exe" tools/turzx-probe/probe.py
```

Send the test after closing TURZX:

```powershell
& "$probeDir\venv\Scripts\python.exe" tools/turzx-probe/probe.py --driver-root "$probeDir\driver" --port COM5 --send
```

Results are written to `$probeDir\results`: the two diagnostic images, the driver
log, and `probe-result.json` with the device ID and raw status response prefixes.
Receiving status bytes is not proof that the picture is correct: check the
orientation labels, RGB swatches, and that `FRAME 01` changes to `UPDATE 02` in
the same bottom-left rectangle. The script does not change firmware, brightness,
theme files, or startup configuration. It sends no reset command.

## Initial hardware result

On 2026-09-16 the connected display replied `chs_88inch.dev1_rom1.90`.
It acknowledged the full frame with `full_png_sucess` (the device's spelling),
and returned `needReSend:0` after the full frame and the partial update.
The user confirmed correct landscape orientation and `UPDATE 02` in the
bottom-left rectangle. Both full-frame output and a visible partial update have
therefore been verified on this unit. Live Discord data and the Earth theme
reconstruction remain separate implementation steps.
