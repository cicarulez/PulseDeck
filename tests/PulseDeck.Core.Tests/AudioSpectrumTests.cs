using PulseDeck.Core;
using Xunit;

public class AudioSpectrumTests
{
    private static byte[] Tone(double frequency)
    {
        var bytes = new byte[AudioSpectrumAnalyzer.Size * 4];
        for (int i = 0; i < AudioSpectrumAnalyzer.Size; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / AudioSpectrumAnalyzer.SampleRate) * 16000);
            bytes[i * 4] = bytes[i * 4 + 2] = (byte)sample;
            bytes[i * 4 + 1] = bytes[i * 4 + 3] = (byte)(sample >> 8);
        }
        return bytes;
    }
    [Fact]
    public void FrequencyBandsDistinguishBassFromTrebleAndDecayToSilence()
    {
        var low = new AudioSpectrumAnalyzer(); var high = new AudioSpectrumAnalyzer();
        var bass = low.AddPcm16(Tone(100), AudioSpectrumAnalyzer.Size * 4)!;
        var treble = high.AddPcm16(Tone(8000), AudioSpectrumAnalyzer.Size * 4)!;
        Assert.Equal(24, bass.Length);
        Assert.True(Array.IndexOf(bass, bass.Max()) < Array.IndexOf(treble, treble.Max()));
        Assert.True(bass.Max() > .5f && treble.Max() > .5f);
        for (int i = 0; i < 30; i++) bass = low.AddPcm16(Tone(100), AudioSpectrumAnalyzer.Size * 4, silent: true)!;
        Assert.All(bass, b => Assert.InRange(b, 0, .001f));
        Assert.False(new DeckConfig().MusicSpectrum);
    }
}
