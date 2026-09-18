# Contributing to PulseDeck

Thanks for helping build a useful display companion. Small, focused contributions are welcome: code, translations, documentation and well-described hardware reports all count.

**No panel? You can still help.** Core and rendering tests run on Linux. The Angular configurator builds without the Windows agent. Only live sensors, media, Discord and USB checks require Windows and the relevant hardware/account.

## Before you start

Check [the roadmap](docs/roadmap.md) and existing [issues](https://github.com/cicarulez/PulseDeck/issues). Open an issue before changing architecture, adding a new transport or starting a large feature. For small fixes, a pull request with context is enough.

Use English for shared documentation and pull requests where possible. Italian reports are welcome. Follow our [code of conduct](CODE_OF_CONDUCT.md). Report security issues through [SECURITY.md](SECURITY.md).

## Development requirements

- **.NET 10 SDK** for portable tests and the Windows agent.
- **Node.js 24.15 or newer in the 24.x line**, with npm, for Angular 22.
- **Python 3** for the WSL/Linux build script's pinned dependency downloads.
- Windows x64 for integration checks; WSL/Linux is supported for editing, tests and cross-publishing.

```sh
git clone https://github.com/cicarulez/PulseDeck.git
cd PulseDeck
```

## Run focused checks

From the repository root:

```sh
dotnet test tests/PulseDeck.Core.Tests/PulseDeck.Core.Tests.csproj -c Release
dotnet test tests/PulseDeck.Rendering.Tests/PulseDeck.Rendering.Tests.csproj -c Release
node --test tests/youtube-extension/*.test.mjs
node --test apps/desktop/policy.test.cjs
```

For the configurator:

```sh
cd apps/configurator
npm ci
npm run build
```

The Angular app expects the local agent API for live interaction. A frontend build by itself does not emulate Windows providers. For reproducible offline documentation fixtures, see [the gallery tool](tools/docs-gallery/README.md).

## Build the Windows package

On WSL/Linux:

```sh
./scripts/build.sh
```

On Windows PowerShell, from the repository root:

```powershell
.\scripts\build.ps1
```

Both produce **`artifacts/windows`**, including the agent, .NET runtime, configurator, launch scripts, native dependencies and notices. Copy the **whole directory** to Windows. Then follow [getting started](docs/getting-started.md).

The first build downloads pinned Discord.Net source and gaming dependencies, with SHA-256 verification. Network access is required. Build output, runtime data and downloaded upstream code belong outside version control.

## Find your way around

| Path | Responsibility |
| --- | --- |
| `apps/agent` | Windows host, providers, rendering and serial transport |
| `apps/configurator` | Angular pages, state and reusable components |
| `apps/youtube-extension` | Optional main-player browser media bridge |
| `apps/desktop` | Optional Electron window and navigation policy |
| `src/PulseDeck.Core` | Portable contracts, validation, profile rules and protocol |
| `src/PulseDeck.Discord` | Pinned upstream WebSocket source with documented roster fix |
| `tests` | Core, renderer, provider and extension regressions |
| `scripts` | Build, launch, startup and packaging |
| `tools/docs-gallery` | Production-renderer fixtures for public screenshots |
| `docs` | Setup, architecture, roadmap and validation evidence |

## Keep changes reviewable

- Use standalone Angular feature components, shared controls and existing styles. Keep feature state in its feature.
- Preserve the shared renderer: preview and physical display must use the same composition.
- Show missing data as unavailable. Do not add simulated readings to the running application.
- Identify sensors by **ID plus name**: some LibreHardwareMonitor IDs collide.
- Keep foreground selection, session timing and media identity separate. Alt-Tab must not reset a running game's timer.
- Explain the user-visible change and run checks relevant to it. Add regression tests for behavior changes; avoid tests that only repeat an implementation.
- Use Conventional Commits, such as `fix(media): clear stale lyrics after a track change`.

## Windows and hardware validation

Run the agent in the signed-in user's session. Media, foreground windows and DPAPI credentials are user-scoped. Automatic startup uses an elevated interactive-user scheduled task, not a LocalSystem service.

Never run the vendor TURZX software and PulseDeck against the same serial port. Device identity must be verified before writes. Do not flash firmware. The verified full-frame command is `C8 EF 69 00 38 40 0E 10`; protocol changes need evidence and regression tests.

Before replacing a running Windows build, stop the agent and wait for both its process and scheduled task to exit. Preserve configuration and credentials. See [hardware notes](docs/hardware.md).

Record actual observations in [validation.md](docs/validation.md): environment, actions, results and limitations. A passing build does not establish working USB, speaking transitions, media selection or sensor readings.

## Submit a pull request

Describe the problem, resulting behavior and validation. Include a screenshot for visible changes, using sanitized or synthetic data. State which Windows/hardware checks you could not run. Use the supplied pull request template.

Do not include credentials, tokens, `.env` files, runtime backups, logs with private data, vendor software, downloaded lyrics or game/album artwork. Contributions are made under the project's **GPL-3.0-or-later** license; retain upstream notices and identify third-party material.
