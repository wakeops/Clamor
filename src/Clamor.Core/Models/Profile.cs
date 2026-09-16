namespace Clamor.Core.Models;

/// <summary>
/// A saved set of sound-button assignments — clip paths, positions, hotkeys, and per-clip
/// volume. Deliberately holds nothing device-related; device selection lives in
/// <see cref="AppSettings"/> since it's app-level, not per-profile.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "Default";

    public List<SoundClip> Clips { get; set; } = new();
}
