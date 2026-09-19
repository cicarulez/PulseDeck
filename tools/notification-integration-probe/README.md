# Notification integration checks

Build on WSL with the .NET 10 SDK:

```sh
dotnet publish tools/notification-integration-probe/PulseDeck.NotificationIntegrationProbe.csproj \
  -c Release -r win-x64 --self-contained true -o artifacts/notification-integration-probe
```

Run `PulseDeck.NotificationIntegrationProbe.exe` in the signed-in Windows user's
session. It uses the production Gmail service/DPAPI store with a synthetic HTTP
transport and a temporary data directory. It checks encryption/reload, the
requested scope, PKCE verifier/challenge, loopback state rejection, callback
completion, baseline, restart/refresh and disconnect. It never opens the browser,
contacts Google or accesses the display. Synthetic tokens are not secrets.

`Invoke-RuntimeProbe.ps1` is a separate **physical display test**, intended to run
against the normal published agent after its build has been safely deployed. It
requires the verified connected panel in Desktop, then triggers three explicitly
labelled eight-second synthetic notification tests through the production API.
Pass `-Kind calendar` to check the transient calendar icon instead of mail counts.
The agent retains port ownership and normal providers throughout. The script
checks live background ticks, badge 3 then 0, final test removal, collection/render
counts, frame acknowledgements and recovery delta. No config or credentials are
changed; the test expires inside the agent even if the script is interrupted.

A passed synthetic OAuth check and a passed panel test do not prove live Gmail
consent or arrival/read behavior. Complete personal OAuth and verify those
separately. See [setup](../../docs/notifications.md) and
[recorded evidence](../../docs/validation.md).
