using System.Text.Json;
using PulseDeck.Agent.Providers;
if (args.Length != 2 || args[0] != "--capture") return 2;
var self = args[1] == "self";
var pid = Environment.ProcessId;
if (!self && (!int.TryParse(args[1], out pid) || pid <= 0)) return 2;
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(6));
int frames = 0; long samples = 0; float peak = 0;
try
{
    await Task.Run(() => ProcessAudioCapture.Run(pid, stop.Token, (bands, count) => { frames++; samples = count; peak = Math.Max(peak, bands.Max()); }));
    Console.WriteLine(JsonSerializer.Serialize(new { pid, self, frames, samples, peak, audioSaved = false }));
    return self ? peak == 0 ? 0 : 1 : frames > 0 ? 0 : 1;
}
catch (Exception e) { Console.WriteLine(JsonSerializer.Serialize(new { error = e.GetType().Name, e.Message, stage = e.Data["stage"], hresult = $"0x{e.HResult:X8}" })); return 1; }
