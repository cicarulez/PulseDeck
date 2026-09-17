using System.Globalization;
using System.Text;

namespace PulseDeck.Core;

/// <summary>PresentMon v1 metrics: application Present rate, not displayed/generated FPS.</summary>
public sealed class PresentMonSamples(int processId)
{
    private string[] header = [];
    private readonly Queue<(DateTimeOffset At, string SwapChain, double Ms)> samples = new();
    private readonly object sync = new();

    public void Add(string line, DateTimeOffset now)
    {
        var fields = Csv(line);
        lock (sync)
        {
            if (fields.Contains("MsBetweenPresents") && fields.Contains("ProcessID")) { header = fields; return; }
            if (fields.Length != header.Length) return;
            string Field(string key) { var index = Array.IndexOf(header, key); return index >= 0 ? fields[index] : ""; }
            if (!int.TryParse(Field("ProcessID"), out var pid) || pid != processId
                || !double.TryParse(Field("MsBetweenPresents"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
                || !double.IsFinite(ms) || ms <= 0 || ms > 10000) return;
            samples.Enqueue((now, Field("SwapChainAddress"), ms));
            while (samples.Count > 4096) samples.Dequeue();
        }
    }

    public FpsSnapshot Read(DateTimeOffset now)
    {
        lock (sync)
        {
            while (samples.TryPeek(out var first) && now - first.At > TimeSpan.FromSeconds(2)) samples.Dequeue();
            var main = samples.GroupBy(s => s.SwapChain).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (main is null || main.Count() < 2) return new("waiting", ProcessId: processId, Detail: "In attesa di fotogrammi del gioco");
            var ms = main.Average(s => s.Ms);
            return new("connected", 1000 / ms, ms, processId, "PresentMon · FPS applicazione (Present), swap chain principale");
        }
    }

    private static string[] Csv(string line)
    {
        var fields = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString()); return fields.ToArray();
    }
}
