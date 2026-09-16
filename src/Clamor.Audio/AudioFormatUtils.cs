using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Clamor.Audio;

/// <summary>
/// Normalizes arbitrary-format sample providers (clip files, mic capture) onto the engine's
/// single mixing format. <see cref="NAudio.Wave.SampleProviders.MixingSampleProvider"/> requires
/// every input to share the mixer's exact <see cref="WaveFormat"/>, so this runs once per
/// source at setup time rather than per-buffer.
/// </summary>
internal static class AudioFormatUtils
{
    public static ISampleProvider MatchChannels(ISampleProvider source, int channels)
    {
        if (source.WaveFormat.Channels == channels)
        {
            return source;
        }

        if (source.WaveFormat.Channels == 1 && channels == 2)
        {
            return new MonoToStereoSampleProvider(source);
        }

        if (source.WaveFormat.Channels == 2 && channels == 1)
        {
            return new StereoToMonoSampleProvider(source);
        }

        throw new NotSupportedException(
            $"Cannot convert {source.WaveFormat.Channels}-channel audio to {channels} channels.");
    }

    public static ISampleProvider MatchSampleRate(ISampleProvider source, int sampleRate) =>
        source.WaveFormat.SampleRate == sampleRate ? source : new WdlResamplingSampleProvider(source, sampleRate);
}
