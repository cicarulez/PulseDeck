# Getting started

PulseDeck runs on **Windows x64** in your signed-in session. You can configure it and use the preview without a physical panel. The current physical-display adapter is verified only for the [supported TURZX revision](hardware.md).

## Obtain a complete build

If a Windows archive is available on [GitHub Releases](https://github.com/cicarulez/PulseDeck/releases), extract it to a permanent local folder. Otherwise, [build from source](../CONTRIBUTING.md#build-the-windows-package) and copy `artifacts/windows` to Windows.

Keep the entire folder together: `PulseDeck.Agent.exe`, its libraries, `wwwroot`, `tools`, `licenses` and the launch scripts. Copying only the executable will not work. Runtime data is stored separately under `%LOCALAPPDATA%\PulseDeck`, unless `PULSEDECK_DATA_DIR` is set.

## First launch

1. Run `Start-PulseDeck.cmd` from the published folder. Its PowerShell launcher requests administrator rights and opens the configurator.
2. Open **http://127.0.0.1:5178** if the browser did not open.
3. In **Sensori**, inspect the available readings. In **Widget**, select bindings and styles, then save.
4. Preview your layout. Closing the browser leaves the agent running.
5. To connect the supported panel, fully close the vendor TURZX app, select the panel's actual COM port, then choose **Collega display**.

To adjust panel brightness, open **Configurazione → Display e musica**, select
**Imposta con PulseDeck**, move the 0–100% slider and save. The chosen brightness
is saved and reapplied when the display reconnects. **Mantieni il valore attuale**
is the default for existing configurations and leaves the panel's current value
in place; it does not restore an earlier brightness setting.
With **Segui Aura Sync** enabled and a manual brightness saved, **Scuro (OFF)**
temporarily sets the panel brightness to 0%, except in the **Gaming** profile,
which keeps the saved brightness. Entering gaming restores that value even if
Aura is still dark; leaving gaming makes the panel follow Aura again. This applies
to both automatic and manually selected gaming. Reapplying an active Aura effect
also restores the saved value. An unavailable Aura connection does not turn off the
panel. This power-state reading was checked against LightingService on the
development PC; other Armoury Crate versions may not expose the same state.

COM5 is the development machine's port, not a universal setting. PulseDeck checks the USB/device identity before transmitting.

To start without elevation, use `Start-PulseDeck.ps1 -StandardUser`. Some sensors and FPS collection may be unavailable. Missing readings are shown as `—`, not replaced with sample values.

## Hardware sensors

PulseDeck includes LibreHardwareMonitor as a library. You do not need its separate desktop application. CPU, motherboard and fan sensors may require **PawnIO plus administrator rights**. Obtain PawnIO from its [official project](https://github.com/namazso/PawnIO.Setup/releases); the development machine was validated with the standard 2.2.0 installation. Restart the agent after installation.

Sensor names and availability depend on the board, firmware and drivers. Zero RPM can be a valid stopped-fan reading. If a reading disappears, inspect the hardware diagnostic status before rebinding it. [Troubleshooting](troubleshooting.md) covers recovery and evidence to include in a report.

## Choose integrations

| Feature | Setup |
| --- | --- |
| Windows media | Start a compatible player. No Spotify API key is needed. |
| Spotify lyrics | Keep **Testi nel profilo Musica** enabled. Only the Spotify Windows session activates lyrics; availability depends on LRCLIB. |
| YouTube selection | Load the optional `youtube-extension` folder in Chrome developer mode. See [the extension guide](../apps/youtube-extension/README.md). Windows media remains the fallback. |
| Weather | In **Configurazione**, search for and select a location. Weather is used by the weather layout. |
| News | Select RSS/Atom channels and enable the footer. Headlines are attributed and link to their publisher. |
| Steam/EA games | Enable discovery, add nonstandard library roots and run **Cerca nuovi giochi**. Review ambiguous candidates before confirming. |
| Game images | Optionally connect your SteamGridDB key in **Configurazione → Display e aspetto**. Steam lookup remains a fallback; manual backgrounds take precedence. |
| Discord | Configure a bot for your server/channel using the credential import described below. Speaking activity additionally requires the bot's voice connection. |

For newly discovered games, wait until FPS collection is **ready before launching the game**. Some anti-cheat protected games prevent starting the collector after launch. FPS means application-presented frames; unavailable metrics remain explicit. The session timer follows the running process, so Alt-Tab does not reset it.

Automatic priority is Gaming → Music → Desktop. Gaming follows the foreground game after the configured delay. Spotify gets an eight-second minimum exit delay to bridge track transitions. A manual profile overrides automatic selection.

## Discord credentials

The current credential setup uses a local import script rather than a public login flow. Create a private folder **outside the repository** with a `config.json` containing the following keys:

```json
{
  "botToken": "YOUR_BOT_TOKEN",
  "guildId": "YOUR_SERVER_ID",
  "voiceChannelId": "YOUR_VOICE_CHANNEL_ID"
}
```

Run the packaged script from the same Windows account that will run PulseDeck:

```powershell
.\Import-Discord.ps1 -SourcePath 'C:\path\to\your\private-import-folder'
```

The script writes DPAPI-encrypted `discord.credentials` to the runtime directory. Remove the plaintext import file after successful import. Never commit or share it. Restart the agent if needed, choose embedded Discord mode and select the tracked participant in the configurator. Use a bot token, never a personal user token.

The bot needs access to the target server/channel; voice activity requires channel connection permission and a working voice connection. Follow the [Discord developer documentation](https://discord.com/developers/docs/intro) to create and authorize a bot. Enable speaking activity in the configurator and set your tracked user ID: the bot joins the configured channel when you enter and leaves when you exit, regardless of profile. Without a tracked ID, it joins while any human participant is present. A muted/deafened flag is not evidence that someone is speaking.

The Discord settings now list voice channels in the imported server and known
non-bot members by display name. Select a channel and primary user, then save;
changes apply without restarting or reimporting credentials. Use **Aggiorna canali
e utenti** after someone joins voice. If they are not in the bot's cache, retain
or enter their ID under **Configurazione tramite ID**. Clearing the channel override
returns to the initially imported channel. The bot remains connected to the server
Gateway while outside voice and receives membership changes as events.

## Google Calendar

Connect the private iCal subscription link under **Configurazione → Calendario**.
See the [calendar guide](calendar.md) for setup, display priority and refresh limits.

## Automatic startup and stopping

From the permanent published folder, run `Install-PulseDeckStartup.ps1` in an elevated PowerShell window. Its parameters identify the legacy TURZX startup task to back up and disable. It creates an **interactive-user scheduled task**, starts hidden at sign-in and connects the configured display without opening the browser. Do not replace it with a LocalSystem service.

- **Scollega display** releases the serial port and leaves the last image on the panel.
- **Arresta PulseDeck** stops the agent and attempts to switch off the panel.
- Closing the browser, or the optional Electron window, leaves the agent running.

To update a build, stop the agent, wait for its process and startup task to exit, then replace the application folder. Preserve the separate runtime directory. Do not overwrite DLLs while the agent is running.

For a source checkout on Windows or WSL, `scripts/Publish-Deploy-PulseDeck.ps1` automates
the full update. Sign in as the Windows user who runs PulseDeck. For a Windows
checkout, open an elevated PowerShell in the checkout and run the command below. The script finds the
installation from the `PulseDeck` scheduled task and verifies its user and path.
Press Enter at the version prompt to keep the current source version. It runs
the tests and publish, backs up the task, prepares a complete new application
folder, stops and waits for the running agent and task, swaps the folders,
starts the task and verifies the expected version through `/api/health`.
If the new version fails to start, it attempts to restore and restart the old
installation. The old folder remains beside the installation as a backup after
a successful update; runtime data in `%LOCALAPPDATA%\PulseDeck` is untouched.

```powershell
.\scripts\Publish-Deploy-PulseDeck.ps1
```

For a WSL checkout, obtain the Windows path with
`wslpath -w scripts/Publish-Deploy-PulseDeck.ps1` in WSL, then run it from an
elevated Windows PowerShell with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '<path returned by wslpath>'
```

The script builds with the SDK and Node
installed in WSL, then deploys in the signed-in Windows session. Windows will
ask for administrator approval when opening the elevated PowerShell.

The script requires .NET SDK 10.x and Node.js 24.15+. Use `-Version 0.8.1` to
set the agent, configurator and npm lockfile version without the interactive
prompt, or `-SkipVersionPrompt` to keep the checked-in version. Changes to the
source version remain in the checkout even if a later build fails.

## Optional desktop window

The main Windows package uses the browser configurator. An Electron wrapper can be built separately with `scripts/build-desktop.ps1` or `scripts/build-desktop.sh`. Its install script is emitted under `artifacts/desktop`; it expects the agent/startup task to be installed already. The wrapper has its own package version and is not required to use PulseDeck.
