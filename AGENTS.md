# Working on PulseDeck

PulseDeck is a Windows display agent for a TURZX 8.8-inch 1920x480 panel.
The repository can be edited and cross-built in WSL; Windows integration checks
must run in the signed-in Windows user's session.

## Structure and implementation

- `apps/agent`: .NET 10 Windows host, providers, SkiaSharp renderer, serial adapter.
- `apps/configurator`: Angular 22 standalone components with strict templates.
  Reuse shared components/styles; keep feature state and markup in feature components.
- `src/PulseDeck.Core`: portable contracts, validation, profile selection and protocol.
- `tests/PulseDeck.Core.Tests`: profile and hardware-protocol regression tests.
- `scripts`: publishing, interactive/background launch, Discord import and startup setup.
- `docs/validation.md`: evidence from actual Windows and physical-display checks.
- `docs/roadmap.md`: agreed priorities and next steps. Aura must initially follow PC
  colors in read-only mode; do not acquire RGB control as a substitute for reading.

The renderer is shared by the preview and physical screen. Show unavailable data
explicitly; do not substitute demo readings. Discord mute/deaf is not speaking.
LibreHardwareMonitor source IDs can collide: NVIDIA GPU Bus and GPU Memory are a
known example. Preserve both readings and use ID plus name for UI row tracking.

## Build and validation

Requirements: .NET 10 SDK and Node 24.15+ compatible with Angular 22.

```sh
dotnet test tests/PulseDeck.Core.Tests/PulseDeck.Core.Tests.csproj -c Release
dotnet publish apps/agent/PulseDeck.Agent.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows
# In apps/configurator:
npm ci
npm run build
```

`scripts/build.sh` and `scripts/build.ps1` assemble the complete published app,
including `wwwroot` and launch/setup scripts. Use targeted checks for changed code;
build success alone does not verify sensors, media, Discord or USB on Windows.
Record limitations honestly, including whether voice transitions were observed.

## Hardware and runtime constraints

- Keep the verified full-frame command `C8 EF 69 00 38 40 0E 10` unchanged unless
  protocol evidence and regression tests justify a change. An extra zero previously
  caused leftover content, wrong dimensions and colored pixel artifacts.
- The tested device is `chs_88inch.dev1_rom1.90`, VID/PID `0525:A4A7`, on COM5.
  Validate device identity before writes. Never flash firmware as part of app work.
- TURZX and PulseDeck must not own the serial port together. Preserve the user's
  settings and display state when updating the running build.
- CPU/motherboard/fan readings may require both PawnIO and administrator rights.
- Run startup through an elevated **interactive-user scheduled task**, not a
  LocalSystem/session-0 service: media, foreground apps and DPAPI are user-scoped.
  Startup is hidden, opens no browser and connects the display. Normal disconnect
  must not trigger an endless reconnect loop.

## Data, deployment and commits

Runtime data belongs under `%LOCALAPPDATA%\PulseDeck` (or `PULSEDECK_DATA_DIR`),
outside source control. Never commit tokens, `.env`, `discord.credentials`, logs,
runtime backups or copied proprietary TURZX/game assets. Discord credentials are
DPAPI CurrentUser-encrypted and must never be returned by the local API.

Before replacing a published Windows build, stop its agent and wait for the process
to exit; locked DLLs can otherwise leave a partial deployment. Back up startup tasks
before replacing them and keep legacy TURZX startup disabled rather than deleting it.

Use Conventional Commits. When the conventional-commit MCP is available, use its
diff analysis and commit tools. Review staged files for secrets and generated output.
