using Clamor.Audio;
using Clamor.Tests.TestSupport;
using NAudio.Wave.SampleProviders;
using Xunit;

namespace Clamor.Tests;

public class AudioFormatUtilsTests
{
    [Fact]
    public void MatchChannels_ReturnsSameInstance_WhenAlreadyMatching()
    {
        var source = new FakeSampleProvider(48000, 2);

        var result = AudioFormatUtils.MatchChannels(source, 2);

        Assert.Same(source, result);
    }

    [Fact]
    public void MatchChannels_WrapsMonoToStereo()
    {
        var source = new FakeSampleProvider(48000, 1);

        var result = AudioFormatUtils.MatchChannels(source, 2);

        Assert.IsType<MonoToStereoSampleProvider>(result);
        Assert.Equal(2, result.WaveFormat.Channels);
    }

    [Fact]
    public void MatchChannels_WrapsStereoToMono()
    {
        var source = new FakeSampleProvider(48000, 2);

        var result = AudioFormatUtils.MatchChannels(source, 1);

        Assert.IsType<StereoToMonoSampleProvider>(result);
        Assert.Equal(1, result.WaveFormat.Channels);
    }

    [Fact]
    public void MatchChannels_Throws_ForUnsupportedConversion()
    {
        var source = new FakeSampleProvider(48000, 6);

        Assert.Throws<NotSupportedException>(() => AudioFormatUtils.MatchChannels(source, 2));
    }

    [Fact]
    public void MatchSampleRate_ReturnsSameInstance_WhenAlreadyMatching()
    {
        var source = new FakeSampleProvider(48000, 2);

        var result = AudioFormatUtils.MatchSampleRate(source, 48000);

        Assert.Same(source, result);
    }

    [Fact]
    public void MatchSampleRate_WrapsResampler_WhenRatesDiffer()
    {
        var source = new FakeSampleProvider(44100, 2);

        var result = AudioFormatUtils.MatchSampleRate(source, 48000);

        Assert.IsType<WdlResamplingSampleProvider>(result);
        Assert.Equal(48000, result.WaveFormat.SampleRate);
    }
}
