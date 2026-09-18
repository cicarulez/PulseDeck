# Google Calendar

PulseDeck reads one Google Calendar through its private iCalendar subscription
link. It does not sign in to your Google account or create, edit or delete events.

## Connect

1. Open [Google Calendar settings](https://calendar.google.com/calendar/u/0/r/settings)
   in a desktop browser.
2. Select the calendar under **Settings for my calendars**, then **Integrate calendar**.
3. Copy **Secret address in iCal format**. Treat this link as a credential: anyone
   with the link can read the calendar. Do not make the calendar public to use PulseDeck.
4. Open PulseDeck's configurator at `http://127.0.0.1:5178`, then
   **Configurazione → Calendario**. Paste the link into the password field and
   choose **Collega Google Calendar**.

The connection is verified before saving and applies immediately. The display
options (**Mostra nello spazio libero**, **Mostra solo “Impegno”**) use the main
**Salva configurazione** button. Some managed Google accounts do not expose the
secret address; see [Google's subscription guide](https://support.google.com/calendar/answer/37648).

## Display behavior

- In Desktop, connected media sessions—including paused sessions—keep their panel.
- When you are in the selected Discord channel without media, the expanded roster
  keeps priority. Otherwise Calendar fills the upper half above the small roster.
- The calendar shows the next timed appointment, prioritizing one already in
  progress. All-day entries are the fallback when no timed appointment remains.
- The panel shows start/end time, a countdown or **IN CORSO**, and the last refresh
  time. **Hide titles** replaces titles with **Impegno** in both the display and
  the current API snapshot.
- Gaming and Spotify's lyrics composition are unchanged. Disabling Calendar
  restores the empty Media Session panel.

## Refresh and limits

PulseDeck refreshes every five minutes; Google's subscription feed may itself be
delayed, so this is not a real-time reminder or push-notification service. The
next seven days are evaluated, including ordinary recurring events, exceptions,
moved/cancelled instances, timezone changes and all-day dates. Timed events use
the PC's display timezone; date-only events retain their calendar date locally.

A failed update clears unavailable event data and retries after two minutes.
The reader accepts only HTTPS iCalendar URLs on `calendar.google.com`, without
redirects. Calendars above 4 MiB or 3,000 event components, more than 10,000
occurrences in the evaluation window, and second/minute recurrence rules are
reported unavailable. At most 100 upcoming occurrences are held for display.

The URL is encrypted using Windows DPAPI CurrentUser in `calendar.credentials`,
never returned by the API or logged by the calendar HTTP client. Downloaded
calendar data is parsed in memory; descriptions, attendees, meeting URLs and
attachments are not exposed, persisted or fetched. See [privacy](privacy.md).

**Rimuovi collegamento** deletes the local credential and clears the panel on the
next update. If the secret link was exposed elsewhere, reset it in Google Calendar
and connect its replacement in PulseDeck.
