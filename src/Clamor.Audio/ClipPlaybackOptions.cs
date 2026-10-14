using Clamor.Core.Models;

namespace Clamor.Audio;

/// <summary>Everything one playback of a clip needs beyond its file path.</summary>
public readonly record struct ClipPlaybackOptions(float Volume, TimeSpan Start, TimeSpan? End, double Speed)
{
    public static ClipPlaybackOptions Default => new(1f, TimeSpan.Zero, null, 1.0);

    public static ClipPlaybackOptions From(double volume, ClipPlaybackSettings playback)
    {
        var normalized = playback.Normalized();
        return new ClipPlaybackOptions(
            (float)Math.Clamp(volume, 0.0, 1.0),
            TimeSpan.FromSeconds(normalized.StartSeconds),
            normalized.EndSeconds is double end ? TimeSpan.FromSeconds(end) : null,
            normalized.Speed);
    }
}
