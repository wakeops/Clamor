using Clamor.Core.Models;
using Xunit;

namespace Clamor.Tests;

public class ClipPlaybackSettingsTests
{
    [Theory]
    [InlineData(0.1, 0.25)]
    [InlineData(1.5, 1.5)]
    [InlineData(5.0, 3.0)]
    public void Normalized_ClampsSpeed(double speed, double expected)
    {
        var result = new ClipPlaybackSettings { Speed = speed }.Normalized();

        Assert.Equal(expected, result.Speed);
    }

    [Fact]
    public void Normalized_ClearsEnd_WhenNotAfterStart()
    {
        var result = new ClipPlaybackSettings { StartSeconds = 2, EndSeconds = 1 }.Normalized();

        Assert.Equal(2, result.StartSeconds);
        Assert.Null(result.EndSeconds);
    }

    [Fact]
    public void Normalized_ClampsNegativeStart()
    {
        var result = new ClipPlaybackSettings { StartSeconds = -1, EndSeconds = 3 }.Normalized();

        Assert.Equal(0, result.StartSeconds);
        Assert.Equal(3, result.EndSeconds);
    }

    [Fact]
    public void Normalized_KeepsRandomWindowOrderedAndAboveFloor()
    {
        var result = new ClipPlaybackSettings { RandomMinDelaySeconds = 0, RandomMaxDelaySeconds = -5 }.Normalized();

        Assert.Equal(ClipPlaybackSettings.MinRandomDelaySeconds, result.RandomMinDelaySeconds);
        Assert.Equal(ClipPlaybackSettings.MinRandomDelaySeconds, result.RandomMaxDelaySeconds);
    }

    [Fact]
    public void Normalized_DoesNotMutateOriginal()
    {
        var original = new ClipPlaybackSettings { Speed = 10 };

        original.Normalized();

        Assert.Equal(10, original.Speed);
    }

    [Fact]
    public void NextRandomDelay_StaysWithinWindow()
    {
        var settings = new ClipPlaybackSettings { RandomMinDelaySeconds = 5, RandomMaxDelaySeconds = 10 };
        var random = new Random(1234);

        for (var i = 0; i < 200; i++)
        {
            var delay = settings.NextRandomDelay(random).TotalSeconds;
            Assert.InRange(delay, 5, 10);
        }
    }
}
