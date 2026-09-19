# Notifications and Gmail

PulseDeck reads Gmail locally and adds an authoritative unread-message badge next
to the clock. The approved envelope animation runs only in **Recon / Desktop**.
Gaming and Music still update the badge; returning to Desktop does not replay
suppressed notifications. Animation can also be disabled independently.

## Development connection and future direction

The current personal Google Cloud project / imported JSON flow is provisional for
this development version. Evaluate a project-managed desktop OAuth client so end
users only sign in and consent, without creating Cloud projects, downloading JSON
or publishing their own websites. This alternative is not implemented or promised:
adopt it only if it actually removes that setup and its Google verification and
maintenance requirements are sustainable; otherwise retain the personal flow.
A managed client can keep Google API access and encrypted user tokens local. The
project maintainer would own the public branding/privacy pages and applicable
OAuth verification rather than each end user. Local-only personal use does not
remove Google's external-production Branding requirements.

## Personal OAuth setup

1. Create a personal project in [Google Cloud](https://console.cloud.google.com/)
   and enable Gmail API.
2. Configure Google Auth Platform's branding and audience. For a personal Gmail
   account, select External and add yourself as a test user while in Testing.
3. Add only `https://www.googleapis.com/auth/gmail.metadata` to Data Access.
4. Create an OAuth client of type **Desktop app** and download its JSON.
5. On the Windows PC running PulseDeck, open Configuration → Notifications,
   import that JSON and choose **Collega Gmail con Google**. Complete consent in
   the browser window. The configurator reports connection status and the first
   unread count. Do not put the JSON, tokens or authorization codes in chat or Git.
6. Save configuration to apply display options and the polling interval (default
   30 seconds, range 15–300). Import/connect/remove take effect immediately.

Google documents [desktop authorization with a loopback callback and PKCE](https://developers.google.com/identity/protocols/oauth2/native-app).
PulseDeck uses a random loopback port, state validation, S256 PKCE, a five-minute
consent deadline and offline token refresh. Endpoints are fixed to Google; URLs
inside an imported client JSON are never used for network requests. The browser
must run on the same Windows PC as the agent.

`gmail.metadata` supports history and counts without access to bodies or
attachments. It is the narrow read-only scope suitable for detecting arrivals;
`gmail.labels` alone cannot read message history. Metadata remains a
[restricted Google scope](https://developers.google.com/workspace/gmail/api/auth/scopes).
A distributed public OAuth client needs the applicable Google verification;
being open source does not itself remove this requirement. This initial flow uses
an individually configured project, not a bundled shared client or a hosted relay.
In External/Testing, [Google refresh tokens expire after seven days](https://developers.google.com/identity/protocols/oauth2#expiration).
Reconnect when prompted without reimporting the JSON. A 403 stating that access
is limited to testers means the exact sign-in account must be added under
Google Auth Platform → Audience → Test users in the project owning the client.

To leave Testing, complete Branding with app name, support email, real public
homepage and privacy URLs. The logo is optional. Follow Google's
[domain and branding requirements](https://support.google.com/cloud/answer/15549049?hl=en),
then choose Audience → Publish app and reconnect in PulseDeck to obtain a new
production authorization. Publishing is distinct from OAuth verification:
[personal use with fewer than 100 users is exempt](https://support.google.com/cloud/answer/13464323?hl=en),
but can retain the unverified-app warning. This exemption does not waive required
Branding fields. PulseDeck cannot inspect the project's publishing status.
Production removes the Testing-specific weekly expiry, not other revocation or
expiry conditions. Workspace policy can also prevent consent.

The configurator contains the complete Italian walkthrough, production checklist,
troubleshooting and official sources (reviewed 2026-09-19).

The client and refresh token are DPAPI CurrentUser-encrypted in `gmail.credentials`
under `%LOCALAPPDATA%\PulseDeck` (or `PULSEDECK_DATA_DIR`). Access tokens and mailbox
history stay in memory. No credentials appear in config, state or status API
responses; OAuth URLs are returned only to the explicit connect request. Gmail
HTTP logging is disabled. Removing the link deletes local credentials and cancels
pending authorization; revoke the Google-side grant separately in
[Google account connections](https://myaccount.google.com/connections).

## Counts, arrivals and errors

- The badge uses `users.labels.get(INBOX).messagesUnread`, not unread threads or
  a list estimate. It excludes archived unread messages and includes all Inbox categories.
- `users.history.list` identifies new unread arrivals with the INBOX label. All pages are processed;
  the cursor advances only after the history and authoritative count succeed.
  Reading messages, marking old messages unread or deleting them does not by
  itself trigger an arrival. Sent, draft, spam and trash additions do not animate.
- Initial connection, restart and an expired history cursor establish a quiet
  baseline. Existing unread mail is counted without an animation storm.
- Arrivals within one refresh are one event. Consecutive arrival batches within
  one polling interval plus ten seconds are grouped to cover a burst straddling
  two polls. Grouping never restarts an active animation or queues another.
- Failed requests show an unavailable badge (`?`), never a stale count or demo
  value. Retries back off from 60 seconds to at most 15 minutes; invalid refresh
  consent requests a new connection. Each collection has a 20-second budget.
- Gmail polling is independent of hardware/media collection and display FPS.
  Gmail push was not chosen because it requires
  [Cloud Pub/Sub infrastructure](https://developers.google.com/workspace/gmail/api/guides/push).
  Arrival/reading detection therefore has polling latency, not push immediacy.

## Shared runtime and physical check

`NotificationSource`, `NotificationEvent` and `NotificationCenter` separate
connector counts, arrivals and presentation. Calendar additions now publish through the same center with their own calendar
icon. Time-based reminders before an appointment are not implemented yet.

Providers retain their one-second collection timer. A separate rendering loop
reads an immutable latest snapshot plus monotonic animation time. There is one
renderer and synchronous serial writer; a slow write skips elapsed frames rather
than creating a queue. Between animations, rendering returns to approximately
one second. Repeated animation frames retain the sample timestamp, so trend
history does not gain invented sensor samples. Animations explicitly use the
verified full-frame transport; its command bytes are unchanged.

**Prova sul display · dati simulati** exercises the actual runtime, renderer and
USB writer for eight seconds: an envelope arrives, the synthetic badge changes
1 → 3 → 0, and the real state returns automatically. The background continues to
receive real provider updates. The test never replaces the Gmail account or
writes configuration. It requires Desktop and enabled animation; leaving Desktop
cancels it. The local diagnostics API reports collection/render counters for
checking separation. This is a transport/runtime test, not proof of live Gmail.

`tools/notification-integration-probe` checks production OAuth/credential code on
Windows against a synthetic in-memory Google transport, including DPAPI, PKCE,
state rejection, callback completion, refresh and removal. It creates and removes
a temporary data directory and never contacts Google or the physical panel.
See [validation](validation.md) for actual checks and pending live-account tests.

## Newly added calendar events

The existing Google iCal link can now notify when a new future event first appears
in the linked calendar. No Gmail connection or additional OAuth project is needed.
Enable **Nuovi eventi nel calendario** in Configuration → Calendar, with the
calendar feed and general animation enabled. Notifications animate only in Recon /
Desktop. Several additions in the same refresh produce one card. The icon follows
the approved rotation/shrink motion and fades at the clock; there is no invented
calendar unread count and the Gmail badge remains independent.

The tracker compares stable VEVENT UIDs across complete successful feed reads.
Identities cover the entire feed, including events beyond the seven-day visible
window. An event or series already seen does not alert again when its title, time,
recurrence or visible-window membership changes. First load, restart and source
changes establish a silent baseline. Reads while notifications are off still
consume additions; switching back to Desktop does not replay suppressed events.
Temporary fetch errors and omitted/reappearing known entries do not create
repeated arrivals. Historical/cancelled-only additions do not notify. A newly
added recurrence whose start is already past is eligible when it has an occurrence
in the currently evaluated seven-day horizon.

Only hashed identities and their eligibility flags are retained in memory and
are excluded from the API. Raw UIDs, attendees, organizers and private titles are
not included in notification cards. Missing UID properties disable identity-based
notifications for that incomplete fetch, because the parser would otherwise
invent new random identifiers. Identity history is bounded and not persisted;
new events added while the agent is stopped are included in its next quiet
baseline rather than replayed.

This detects **new events in the calendar**, including invitations that Google
has added to it. It cannot detect an invitation that only exists as an email and
has not appeared in the iCal feed. Google's invitation settings may require
responding or confirming the sender before the event appears; see
[Manage invitations in Calendar](https://support.google.com/calendar/answer/13159188).
Calendar collection is configurable from 1 to 60 minutes (default 5), with possible additional Google feed delay. Saving an interval change reschedules from the last completed fetch without resetting event identities or queuing concurrent work. Failed requests wait at least two minutes and never less than the configured interval.

**Prova notifica calendario · simulata** exercises this icon through the actual
agent/display pipeline without creating any appointment. The Windows runtime
probe also accepts `-Kind calendar`. Real invitation addition still needs separate
account-level observation; synthetic tests are not evidence of an actual invite.
