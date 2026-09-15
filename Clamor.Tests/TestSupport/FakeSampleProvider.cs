using NAudio.Wave;

namespace Clamor.Tests.TestSupport;

/// <summary>Minimal ISampleProvider stub — only WaveFormat matters for the format-matching
/// logic under test, so Read just hands back silence.</summary>
internal sealed class FakeSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    public FakeSampleProvider(int sampleRate, int channels)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}
