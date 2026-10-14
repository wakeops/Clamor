namespace Clamor.Core.Models;

/// <summary>
/// Per-clip playback shaping edited from the Playback drawer: which slice of the file plays,
/// how fast, and whether the clip fires on its own at random intervals. Volume stays on
/// <see cref="SoundClip.Volume"/> since the grid's own slider already owns it.
/// </summary>
public sealed class ClipPlaybackSettings
{
    public const double MinSpeed = 0.25;
    public const double MaxSpeed = 3.0;

    /// <summary>Shortest random delay allowed — keeps a misconfigured clip from spamming.</summary>
    public const double MinRandomDelaySeconds = 1.0;

    /// <summary>Offset into the file playback starts from.</summary>
    public double StartSeconds { get; set; }

    /// <summary>Offset playback stops at, or null to play through to the end of the file.</summary>
    public double? EndSeconds { get; set; }

    /// <summary>Playback rate multiplier, <see cref="MinSpeed"/>-<see cref="MaxSpeed"/>.</summary>
    public double Speed { get; set; } = 1.0;

    public bool RandomPlayEnabled { get; set; }

    public double RandomMinDelaySeconds { get; set; } = 30;

    public double RandomMaxDelaySeconds { get; set; } = 120;

    public ClipPlaybackSettings Clone() => (ClipPlaybackSettings)MemberwiseClone();

    /// <summary>
    /// Returns a copy with every field pulled into a playable range: speed clamped, a start
    /// that isn't negative, an end that is null or after the start, and a random delay window
    /// whose minimum is at least <see cref="MinRandomDelaySeconds"/> and never above its maximum.
    /// </summary>
    public ClipPlaybackSettings Normalized()
    {
        var copy = Clone();
        copy.Speed = Math.Clamp(Speed, MinSpeed, MaxSpeed);
        copy.StartSeconds = Math.Max(0, StartSeconds);
        if (copy.EndSeconds is double end && end <= copy.StartSeconds)
        {
            copy.EndSeconds = null;
        }

        copy.RandomMinDelaySeconds = Math.Max(MinRandomDelaySeconds, RandomMinDelaySeconds);
        copy.RandomMaxDelaySeconds = Math.Max(copy.RandomMinDelaySeconds, RandomMaxDelaySeconds);
        return copy;
    }

    /// <summary>Picks the wait before the next random play, uniformly within the delay window.</summary>
    public TimeSpan NextRandomDelay(Random random)
    {
        var normalized = Normalized();
        var span = normalized.RandomMaxDelaySeconds - normalized.RandomMinDelaySeconds;
        return TimeSpan.FromSeconds(normalized.RandomMinDelaySeconds + random.NextDouble() * span);
    }
}
