using Clamor.Core.Models;

namespace Clamor.Core.Services;

/// <summary>
/// CRUD over the currently loaded profile's clip list, persisting each change via
/// <see cref="IProfileService"/> and notifying subscribers so the UI stays in sync.
/// </summary>
public interface IClipLibraryService
{
    Profile CurrentProfile { get; }

    event EventHandler? ClipsChanged;

    void LoadProfile(string name);

    SoundClip AddClip(string filePath, string? displayName = null);

    void RemoveClip(Guid clipId);

    void MoveClip(Guid clipId, int newIndex);

    void UpdateVolume(Guid clipId, double volume);

    void UpdateHotkey(Guid clipId, HotkeyBinding? hotkey);

    void RenameClip(Guid clipId, string name);

    void Save();
}
