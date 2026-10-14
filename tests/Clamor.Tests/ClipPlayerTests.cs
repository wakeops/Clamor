using Clamor.Audio;
using Clamor.Tests.TestSupport;
using NAudio.Wave;
using Xunit;

namespace Clamor.Tests;

public class ClipPlayerTests
{
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    /// <summary>Writes a mono sine wave WAV of the given length.</summary>
    private static string WriteTone(string dir, double seconds, int sampleRate = 48000)
    {
        var path = Path.Combine(dir, "tone.wav");
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        var samples = new float[(int)(seconds * sampleRate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5f;
        }

        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    /// <summary>Drains the player and returns how long its output lasts at the mix rate.</summary>
    private static double PlayedSeconds(ClipPlayer player)
    {
        var buffer = new float[4096];
        long total = 0;
        int read;
        while ((read = player.Output.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
        }

        return (double)total / MixFormat.Channels / MixFormat.SampleRate;
    }

    [Fact]
    public void DefaultOptions_PlayWholeFile()
    {
        using var dir = new TempDirectory();
        var path = WriteTone(dir.Path, 1.0);
        using var player = new ClipPlayer(path, ClipPlaybackOptions.Default, MixFormat);

        Assert.Equal(1.0, PlayedSeconds(player), 2);
    }

    [Fact]
    public void StartAndEnd_TrimPlayback()
    {
        using var dir = new TempDirectory();
        var path = WriteTone(dir.Path, 1.0);
        var options = ClipPlaybackOptions.Default with
        {
            Start = TimeSpan.FromSeconds(0.25),
            End = TimeSpan.FromSeconds(0.75),
        };
        using var player = new ClipPlayer(path, options, MixFormat);

        Assert.Equal(0.5, PlayedSeconds(player), 2);
    }

    [Theory]
    [InlineData(2.0, 0.5)]
    [InlineData(0.5, 2.0)]
    public void Speed_ScalesPlayedLength(double speed, double expectedSeconds)
    {
        using var dir = new TempDirectory();
        var path = WriteTone(dir.Path, 1.0);
        using var player = new ClipPlayer(path, ClipPlaybackOptions.Default with { Speed = speed }, MixFormat);

        Assert.Equal(expectedSeconds, PlayedSeconds(player), 1);
    }

    [Fact]
    public void ReadDuration_ReturnsFileLength()
    {
        using var dir = new TempDirectory();
        var path = WriteTone(dir.Path, 1.5);

        Assert.Equal(1.5, ClipPlayer.ReadDuration(path)!.Value.TotalSeconds, 2);
    }

    [Fact]
    public void ReadDuration_ReturnsNull_ForMissingFile()
    {
        using var dir = new TempDirectory();

        Assert.Null(ClipPlayer.ReadDuration(Path.Combine(dir.Path, "missing.wav")));
    }
}
