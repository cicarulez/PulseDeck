# Notification render experiment

Standalone Gmail-style notification animation, before implementing notification
providers or changing the live display loop. Uses the production panel renderer
for the background and SkiaSharp for all animation frames. The illustrative icon
is drawn with paths; no external assets are downloaded.

Run from the repository root with .NET 10:

```sh
dotnet run --project tools/notification-preview -c Release -- artifacts/notification-preview
python3 -m http.server 5180 --bind 127.0.0.1 --directory artifacts/notification-preview
```

Open http://127.0.0.1:5180. Replay, pause, scrub and compare 30 fps with a
one-frame-per-second sample. The badge is a fixed **synthetic** count of three;
hardware readings remain unavailable. Reduced-motion preferences skip autoplay.

This tool has no providers, credential access, agent API calls or serial writes.
It cannot receive email or change the running display. Browser playback does not
validate physical panel throughput. The production agent currently samples and
renders about once per second; smooth hardware animation will require a separate
timing/transport experiment. Linux may substitute fonts for Windows Segoe UI.

Generated images belong in ignored `artifacts`, never in source control.
