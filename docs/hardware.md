# Hardware compatibility

| Component | Current evidence |
| --- | --- |
| Operating system | Windows x64, signed-in interactive session |
| Panel | TURZX 8.8″, 1920 × 480 |
| Awake identity | `chs_88inch.dev1_rom1.90`, USB VID/PID `0525:A4A7` |
| Standby identity on tested unit | `CT88INCH`, USB `1A86:CA88` |
| Port | User-selectable; COM5 is the tested machine's awake port |
| Other panels | Not verified; 3.5″ and multiple-display support remain future work |
| Sensor access | Hardware/driver-dependent; some CPU, motherboard and fan sensors need PawnIO and elevation |

A matching screen size or enclosure is not enough to establish protocol compatibility. New device support should start with a read-only identity report and documented protocol evidence, not trial writes to unknown serial devices.

## Transport behavior

PulseDeck verifies identity before writes and shares the same frame renderer with the preview. Full frames initialize the acknowledged baseline; partial updates use changes from an acknowledged frame. The verified full-frame command is `C8 EF 69 00 38 40 0E 10`.

Recoverable failures have a bounded retry budget. Repeated resend/timeout failures eventually stop recovery and require an explicit reconnect. A user disconnect must remain disconnected. USB acknowledgement confirms transport progress, not the visual correctness of the picture.

The tested unit changes its serial identity in standby. The explicit connect path can wake that known identity before revalidating the awake device. PulseDeck does not flash firmware or alter vendor themes.

## Report compatibility

Include Windows version, panel model, resolution, USB VID/PID, reported device ID, agent version and what you observed. Remove serial numbers, personal paths and unrelated device identifiers. State whether you tested preview, actual display writes, unplug/replug, sleep/wake or shutdown.

Do not run the TURZX application and PulseDeck on the same serial port at the same time. See [the validation record](validation.md) for actual observations and remaining gaps.
