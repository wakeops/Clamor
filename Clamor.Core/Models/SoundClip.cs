namespace Clamor.Core.Models;

public sealed class SoundClip
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    /// <summary>Linear gain, 0.0 (silent) to 1.0 (full volume).</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Position within the sound grid; lower sorts first.</summary>
    public int SortOrder { get; set; }

    public HotkeyBinding? Hotkey { get; set; }
}
