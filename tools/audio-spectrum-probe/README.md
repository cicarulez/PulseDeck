# Spotify audio spectrum probe

Windows integration check for the same process-loopback capture used by the agent.
It captures a specified process tree for six seconds, analyses samples in memory,
and prints only sample/frame counts and the maximum normalized frequency level.
It does not write audio files, access a microphone or open the display's serial port.

Requires Windows build 20348 or newer and the signed-in user's session. Identify
the root Spotify process (its parent is not another Spotify process); do not use
a random child process or assume a PID survives a restart.

```sh
dotnet publish tools/audio-spectrum-probe/PulseDeck.AudioSpectrumProbe.csproj -c Release -r win-x64 --self-contained true -o artifacts/audio-spectrum-probe
```

On Windows, with Spotify playing:

```powershell
.\PulseDeck.AudioSpectrumProbe.exe --capture <Spotify-root-PID>
```

With Spotify still playing, `--capture self` selects the silent probe's own
process tree. Its peak must remain zero; this checks that unrelated Spotify audio
does not enter the selected process capture without producing extra test sounds.

Success means samples reached the analyser; it does not prove visual fluidity or
isolation during competing audio. Those require additional integration checks.
API reference: [Microsoft application-loopback sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/).
