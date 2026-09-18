# Troubleshooting

## The configurator does not open

Try `http://127.0.0.1:5178`. Run the launcher from the complete published folder, not from the source directory and not from a copy of only the EXE. Check whether `PulseDeck.Agent` is running. A browser closing does not stop the agent.

## Some sensors show `—`

Check **Sensori** for available readings and the administrator/PawnIO diagnostics. Bind the exact sensor by ID and name; similar names can represent different readings. Hardware support varies. PulseDeck performs bounded motherboard reinitialization for configured LPC readings that disappear, but it cannot create sensors absent from the driver.

## The panel is disconnected or reports an unexpected identity

Close the vendor application completely, verify the actual COM port and check [hardware compatibility](hardware.md). An unknown identity is deliberately rejected. After recovery is exhausted, reconnect explicitly. Keep error text and device identity for a sanitized report; do not try firmware flashing as a troubleshooting step.

## FPS are unavailable

Wait for collector readiness before launching the game. A newly installed/discovered game can require a fresh collection filter; close it normally, let PulseDeck prepare, then reopen it. Some anti-cheat protected games prevent attaching after launch. Check that the foreground process is recognized and that the full package includes PresentMon under `tools`.

## Discord roster works but speaking does not

Speaking requires the optional voice connection and is distinct from mute/deaf state. Check voice connection status, bot permissions and network availability. Include whether actual speaking → silence transitions were observed when reporting a problem. Never share voice diagnostic logs without inspecting and removing sensitive content.

## YouTube shows the wrong title

Windows can expose a single aggregated Chrome media session. Install the optional [YouTube extension](../apps/youtube-extension/README.md) to prioritize the main player in playback and ignore thumbnail previews. Without it, PulseDeck keeps the Windows integration and its limitations.

## Spotify lyrics do not appear

Check that the selected media source is the Spotify Windows app and that **Testi nel profilo Musica** is enabled. Browser Spotify and generic media sessions do not activate this feature. LRCLIB may lack the exact recording, or its title/artist/duration may not match. Missing lyrics do not prevent ordinary media display.

## The profile changes while switching apps

Automatic priority is Gaming → Music → Desktop, with a configurable delay. Spotify's minimum eight-second exit delay covers track transitions and also delays returning to Desktop after a genuine pause. A game's timer follows its process lifetime rather than foreground time. A manual profile stays selected until you return to automatic mode.

## Updating fails with locked DLLs

Stop PulseDeck through the configurator. Wait for its process and the scheduled task to exit before copying the new build. Preserve the runtime data directory. If an update was partially copied, replace the entire application folder from a complete package before restarting.

## Reporting a bug

Use the [bug report form](https://github.com/cicarulez/PulseDeck/issues/new?template=bug_report.yml). Include version, minimal steps, expected/actual behavior, preview versus physical-panel results and relevant sanitized diagnostics. A screenshot with fictional data is preferable to one exposing private names, paths or media content.
