using NAudio.Wave;

namespace Clamor.Audio;

/// <summary>
/// Wraps one active playback instance of a single clip. Each button press gets its own
/// <see cref="ClipPlayer"/> so overlapping presses of the same (or different) buttons play
/// independently rather than cutting each other off.
/// </summary>
public sealed class ClipPlayer : IDisposable
{
    private readonly AudioFileReader _reader;

    /// <summary>Sample provider ready to add straight into a mixer matching <c>targetFormat</c>.</summary>
    public ISampleProvider Output { get; }

    public ClipPlayer(string filePath, float volume, WaveFormat targetFormat)
    {
        // AudioFileReader decodes mp3/wav/etc. and always exposes 32-bit IEEE float samples,
        // so only channel count and sample rate ever need matching to the mixer's format.
        _reader = new AudioFileReader(filePath) { Volume = volume };

        ISampleProvider provider = _reader;
        provider = AudioFormatUtils.MatchChannels(provider, targetFormat.Channels);
        provider = AudioFormatUtils.MatchSampleRate(provider, targetFormat.SampleRate);
        Output = provider;
    }

    public float Volume
    {
        get => _reader.Volume;
        set => _reader.Volume = value;
    }

    public void Dispose() => _reader.Dispose();
}
