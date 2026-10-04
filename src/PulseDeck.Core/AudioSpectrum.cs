using System.Numerics;

namespace PulseDeck.Core;

public sealed record AudioSpectrumSnapshot(string Status = "disabled", float[]? Bands = null,
    string? Detail = null, int? ProcessId = null, long Samples = 0);

// A fixed memory window; PCM is analysed in memory and never retained as a recording.
public sealed class AudioSpectrumAnalyzer
{
    public const int Size = 2048, BandCount = 24, SampleRate = 44100;
    private readonly float[] window = new float[Size], smooth = new float[BandCount];
    private readonly Complex[] fft = new Complex[Size];
    private int position;
    public long Samples { get; private set; }
    public float[]? AddPcm16(byte[] pcm, int length, bool silent = false)
    {
        float[]? result = null;
        for (int i = 0; i + 3 < length; i += 4)
        {
            var left = (short)(pcm[i] | pcm[i + 1] << 8);
            var right = (short)(pcm[i + 2] | pcm[i + 3] << 8);
            window[position++] = silent ? 0 : (left + right) / 65536f;
            Samples++;
            if (position == Size) { position = 0; result = Analyse(); }
        }
        return result;
    }
    private float[] Analyse()
    {
        for (int i = 0; i < Size; i++) fft[i] = window[i] * (.5 - .5 * Math.Cos(2 * Math.PI * i / (Size - 1)));
        for (int i = 1, j = 0; i < Size; i++)
        {
            int bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (fft[i], fft[j]) = (fft[j], fft[i]);
        }
        for (int size = 2; size <= Size; size <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / size);
            for (int start = 0; start < Size; start += size)
            {
                var factor = Complex.One;
                for (int j = 0; j < size / 2; j++, factor *= step)
                {
                    var even = fft[start + j]; var odd = fft[start + j + size / 2] * factor;
                    fft[start + j] = even + odd; fft[start + j + size / 2] = even - odd;
                }
            }
        }
        for (int band = 0; band < BandCount; band++)
        {
            var lower = Math.Max(1, (int)(40 * Math.Pow(400, (double)band / BandCount) * Size / SampleRate));
            var upper = Math.Min(Size / 2, Math.Max(lower + 1, (int)(40 * Math.Pow(400, (double)(band + 1) / BandCount) * Size / SampleRate)));
            var amplitude = 0d;
            for (int bin = lower; bin < upper; bin++) amplitude = Math.Max(amplitude, fft[bin].Magnitude * 4 / Size);
            var level = (float)Math.Clamp((20 * Math.Log10(Math.Max(amplitude, 1e-9)) + 70) / 70, 0, 1);
            smooth[band] += (level - smooth[band]) * (level > smooth[band] ? .75f : .3f);
        }
        return (float[])smooth.Clone();
    }
}
