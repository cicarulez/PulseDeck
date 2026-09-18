using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public static class CalendarPanel
{
    public static void Draw(SKCanvas canvas, CalendarSnapshot calendar, CalendarOptions options, DateTimeOffset now,
        float x, float y, float width, SKColor accent, SKTypeface normal, SKTypeface bold)
    {
        var muted = new SKColor(144, 162, 161);
        void Text(string text, float top, float size, SKColor color, bool heavy = false)
        {
            using var font = new SKFont(heavy ? bold : normal, size);
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            if (font.MeasureText(text) > width)
            {
                while (text.Length > 0 && font.MeasureText(text + "…") > width) text = text[..^1];
                text += "…";
            }
            canvas.DrawText(text, x, top, font, paint);
        }
        Text("GOOGLE CALENDAR", y, 15, accent, true);
        var next = calendar.Next(now);
        if (next is null)
        {
            Text(calendar.Status switch { "not-configured" => "Collega il tuo calendario", "loading" => "Caricamento appuntamenti…",
                "connected" or "empty" => "Nessun impegno nei prossimi 7 giorni", _ => "Calendario non disponibile" }, y + 50, 23, SKColors.White, true);
            Text(calendar.Status == "not-configured" ? "Configurazione → Calendario" : calendar.Status == "unavailable"
                ? "Controlla il collegamento nel configuratore" : "Gli appuntamenti compariranno qui", y + 87, 16, muted);
            return;
        }
        var start = next.Start.ToLocalTime();
        var ongoing = !next.AllDay && next.Start <= now && next.End > now;
        var date = start.Date == now.LocalDateTime.Date ? "OGGI" : start.Date == now.LocalDateTime.Date.AddDays(1) ? "DOMANI" : start.ToString("dd/MM");
        Text(next.AllDay ? date + " · TUTTO IL GIORNO" : date + " · " + start.ToString("HH:mm") + " – " + next.End.ToLocalTime().ToString("HH:mm"), y + 33, 18, muted);
        var title = options.HideTitles ? "Impegno" : next.Title;
        using var titleFont = new SKFont(bold, 25);
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = new List<string>(); var row = "";
        foreach (var word in words)
        {
            var candidate = row.Length == 0 ? word : row + " " + word;
            if (row.Length > 0 && titleFont.MeasureText(candidate) > width) { rows.Add(row); row = word; }
            else row = candidate;
        }
        if (row.Length > 0) rows.Add(row);
        for (var i = 0; i < Math.Min(2, rows.Count); i++) Text(rows[i] + (i == 1 && rows.Count > 2 ? "…" : ""), y + 70 + i * 30, 25, SKColors.White, true);
        var until = (ongoing ? next.End : next.Start) - now;
        var minutes = Math.Max(1, (int)Math.Ceiling(until.TotalMinutes));
        var remaining = minutes >= 1440 ? $"{minutes / 1440} g {(minutes % 1440) / 60} h" : minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        Text(next.AllDay ? "EVENTO GIORNALIERO" : ongoing ? "IN CORSO · termina tra " + remaining : "TRA " + remaining, y + 146, 18, accent, true);
        if (calendar.FetchedAt is { } fetched) Text("Aggiornato alle " + fetched.ToLocalTime().ToString("HH:mm"), y + 171, 12, muted);
    }
}
