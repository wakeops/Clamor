using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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
        : this(filePath, ClipPlaybackOptions.Default with { Volume = volume }, targetFormat)
    {
    }

    public ClipPlayer(string filePath, ClipPlaybackOptions options, WaveFormat targetFormat)
    {
        // AudioFileReader decodes mp3/wav/etc. and always exposes 32-bit IEEE float samples,
        // so only channel count and sample rate ever need matching to the mixer's format.
        _reader = new AudioFileReader(filePath) { Volume = options.Volume };

        // Seek rather than skip so a late start point doesn't decode everything before it.
        var total = _reader.TotalTime;
        var start = options.Start < TimeSpan.Zero ? TimeSpan.Zero : options.Start;
        if (start > TimeSpan.Zero && start < total)
        {
            _reader.CurrentTime = start;
        }

        ISampleProvider provider = _reader;
        if (options.End is TimeSpan end && end > start && end < total)
        {
            provider = new OffsetSampleProvider(provider) { Take = end - start };
        }

        provider = AudioFormatUtils.MatchChannels(provider, targetFormat.Channels);
        provider = AudioFormatUtils.ApplySpeed(provider, options.Speed);
        provider = AudioFormatUtils.MatchSampleRate(provider, targetFormat.SampleRate);
        Output = provider;
    }

    public float Volume
    {
        get => _reader.Volume;
        set => _reader.Volume = value;
    }

    /// <summary>Length of the audio file, or null if it can't be opened/decoded.</summary>
    public static TimeSpan? ReadDuration(string filePath)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);
            return reader.TotalTime;
        }
        catch (Exception)
        {
            // Missing, locked, or undecodable — NAudio surfaces each as a different exception
            // type, and the caller only needs to know the length is unknown.
            return null;
        }
    }

    public void Dispose() => _reader.Dispose();
}
