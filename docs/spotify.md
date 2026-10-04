# Optional Spotify account connection

PulseDeck's base Music mode works without a Spotify login: Windows media metadata,
cover art, playback progress and LRCLIB lyrics remain available. Connecting an
account adds the track's full artist list, active device and up to three queued
tracks in the configurator. The dedicated Music panel shows the next queued track.

## Personal setup

1. Create an app in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Select **Web API**. The Web Playback SDK is not needed.
3. Register this exact Redirect URI, including its trailing slash:
   `http://127.0.0.1:5179/spotify/callback/`.
4. In **Users Management**, allow the Spotify account you will connect.
5. Open PulseDeck → **Configurazione → Spotify**, save the public **Client ID**,
   and choose **Collega Spotify — funzionalità extra**.
6. Complete Spotify's consent in the browser. The callback returns to PulseDeck.

No Client Secret is needed. Current development-mode requirements include Premium
for the app owner and a maximum of five allowed users for new apps. Spotify controls
these requirements: see [quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)
and [redirect requirements](https://developer.spotify.com/documentation/web-api/concepts/redirect_uri).
The Client ID is configured per installation, not included in repository defaults.

## Behavior and privacy

The account connection is read-only, using Authorization Code with PKCE and the
`user-read-playback-state` and `user-read-currently-playing` scopes. PulseDeck does
not request playback-control, library or playlist permissions. Login attempts
expire after five minutes; callback state must match the pending attempt.

Refresh tokens are protected with Windows DPAPI CurrentUser in
`%LOCALAPPDATA%\PulseDeck\spotify.credentials` (or `PULSEDECK_DATA_DIR`). The local
API exposes the public Client ID and status, never tokens. **Scollega Spotify**
removes the account token and retains the public Client ID for later reconnection.
You can also revoke permission in [Spotify account apps](https://www.spotify.com/account/apps/).

Playback is checked every ten seconds and the queue at most every thirty seconds,
or on an observed track change. Spotify rate limits honor `Retry-After` and failures
back off. Network work runs independently of the display collector and renderer.

Extras apply only to a fresh Spotify response matching the local title, artist,
album when available and duration. Another player, a different track, a private
session or unavailable/expired data uses the base experience. This version does
not switch the display to a remote phone/speaker session. An unavailable queue is
shown explicitly. Local files and podcasts are not enriched.

The original local artist stays available for lyrics lookup and cache identity.
The full artist list changes presentation only. Lyrics still come from LRCLIB;
Spotify login does not supply lyrics or guarantee lyric availability.

## Validation

Portable OAuth, HTTP, retry, matching and cancellation tests:

```sh
dotnet test tests/PulseDeck.Spotify.Tests/PulseDeck.Spotify.Tests.csproj -c Release
```

The normal build scripts run this suite along with Core and rendering tests.
Actual account consent and DPAPI persistence require the Windows user's session;
see [validation evidence](validation.md) for the checks performed.
