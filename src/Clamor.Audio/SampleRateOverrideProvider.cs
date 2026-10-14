using NAudio.Wave;

namespace Clamor.Audio;

/// <summary>
/// Passes samples through untouched but reports a different sample rate. Feeding that into a
/// resampler is how playback speed is applied: a clip that claims to be 2x its real rate gets
/// resampled down to half as many samples, so it plays twice as fast (and an octave higher,
/// like speeding up a tape).
/// </summary>
internal sealed class SampleRateOverrideProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public SampleRateOverrideProvider(ISampleProvider source, int sampleRate)
    {
        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, source.WaveFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
}
