# Documentation gallery

This tool creates public-safe images using the **actual panel renderer** and **actual configurator**. It is separate from the application and cannot access production providers, credentials or the serial display.

The fixture data, participant names, game/track names, lyrics and procedural artwork are original examples. Do not replace them with a user's runtime data or third-party album/game art for public documentation.

## Render panel images

From the repository root, with .NET 10:

```sh
dotnet run --project tools/docs-gallery -c Release -- artifacts/docs-gallery
```

Copy `desktop.png`, `gaming.png` and `music.png` from that output into `docs/images` after visual review. The tool also writes the synthetic state/config/catalog JSON used by the configurator capture. These fixtures remain under ignored `artifacts`.

For screenshots matching the Windows agent's Segoe UI typography, run the tool on Windows. You can cross-publish from WSL:

```sh
dotnet publish tools/docs-gallery -c Release -r win-x64 --self-contained true -o artifacts/docs-gallery-tool
```

Run `PulseDeck.DocsGallery.exe` on Windows with an output-directory argument. Copy the three panel PNGs to `docs/images` and the three JSON files to `artifacts/docs-gallery` in your checkout. Linux rendering works but may use different fallback fonts. No proprietary fonts need to be redistributed.

## Capture the configurator and covers

Build the frontend first:

```sh
cd apps/configurator
npm ci
npm run build
cd ../..
```

Then install the gallery's isolated browser tooling:

```sh
cd tools/docs-gallery
npm ci
npx playwright install chromium
cd ../..
npm run capture --prefix tools/docs-gallery
```

On Linux CI, browser dependencies may require `npx playwright install --with-deps chromium`. To use an already-installed Chromium binary, set `PULSEDECK_CHROMIUM` to its absolute path.

The capture script starts an ephemeral loopback fixture server and intercepts SignalR inside its isolated browser. It blocks other HTTP origins and does not contact the live agent. It writes `configurator.png`, `hero.png` and `social-preview.png` under `docs/images`. Source HTML for the covers remains in `artifacts/docs-gallery` for inspection.

Keep the sample-data disclosure in the README/gallery when sharing these images. Review the output after renderer changes; fonts and browser rasterization can differ by platform.
