# Internal playback investigation assets

This directory contains an offline test-clip generator and a separate Windows
diagnostic executable. It is not used by the production agent. The live overlay
path is an experiment with a small opaque CPU-reading rectangle.

`--inspect` temporarily disconnects PulseDeck, verifies the USB and HELLO identity
of COM5, and requests media directory listings. The tested firmware automatically
created the missing SD-card directory during this request: inspection is therefore
not guaranteed to be free of storage side effects. PulseDeck reconnects afterward.

`--video <clip-path>` accepts only the original clip's exact size and SHA-256.
It allocates a unique unused filename, uploads the clip, verifies its stored size
and requests 90 seconds of playback without saving it as the startup video.
Cleanup stops playback, deletes only the probe's allocated file and requests the
ordinary PulseDeck connection. On the tested ROM, upload, size check, playback
and removal succeeded. The user reported motion smoother than the live spectrum,
though not comparable to the AMD vendor animation. This is not a measured 24 fps.

`--overlay <clip-path>` repeats that trial and attempts to initialize a transparent
video overlay, then updates one small opaque rectangle with CPU load from the
running agent once per second. Unavailable readings are displayed explicitly.
The experimental CA/D0/18-byte CC composition contract combines the public
5-inch PR with the verified 8.8-inch BGRA dimensions; compatibility must be
established on hardware and must not be inferred from unit tests. A rejection
stops the trial and requests the normal connection after cleaning up its clip.
On ROM 1.90, initialization acknowledged but the first CPU update timed out.
Seeding the update counter from QUERY_STATUS instead returned needReSend:1.
The overlay therefore remains unsupported by this experiment, despite valid
unit-tested packet construction. Both trials removed their own diagnostic clip.

Requires FFmpeg with libx264/drawtext and ffprobe. From WSL:

```sh
bash tools/internal-playback-probe/create-test-clip.sh \
  /mnt/c/Users/cappa/AppData/Local/PulseDeck/refresh-investigation/internal-playback/pulsedeck-playback-test.mp4
```

The original diagnostic animation contains a grid and moving marker, not sensor
readings. Output is a three-second, 72-frame, silent H.264/yuv420p clip at 24 fps,
480×1920 portrait to match the observed vendor media dimensions. Encoding success
does not establish the panel's accepted profile or physical playback rate.
The script refuses an output inside the repository and does not overwrite files.

Next prerequisite: establish the 8.8-inch overlay placement/alpha/count contract.
Any overlay must use an actual provider reading and
must remove old glyphs without hiding or interrupting the video. End any hardware
trial by stopping media and restoring the verified ordinary agent connection.

See [research notes](../../docs/turzx_internal_playback_research.md).
