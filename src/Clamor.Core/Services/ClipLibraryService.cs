using Clamor.Core.Models;

namespace Clamor.Core.Services;

public sealed class ClipLibraryService : IClipLibraryService
{
    private readonly IProfileService _profileService;
    private Profile _currentProfile;

    public Profile CurrentProfile => _currentProfile;

    public event EventHandler? ClipsChanged;

    public ClipLibraryService(IProfileService profileService, string initialProfileName = "Default")
    {
        _profileService = profileService;
        _currentProfile = _profileService.LoadOrCreate(initialProfileName);
    }

    public void LoadProfile(string name)
    {
        _currentProfile = _profileService.LoadOrCreate(name);
        RaiseClipsChanged();
    }

    public SoundClip AddClip(string filePath, string? displayName = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Audio file not found.", filePath);
        }

        var clip = new SoundClip
        {
            Name = displayName ?? Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Volume = 1.0,
            SortOrder = _currentProfile.Clips.Count == 0 ? 0 : _currentProfile.Clips.Max(c => c.SortOrder) + 1,
        };

        _currentProfile.Clips.Add(clip);
        Save();
        RaiseClipsChanged();
        return clip;
    }

    public void RemoveClip(Guid clipId)
    {
        _currentProfile.Clips.RemoveAll(c => c.Id == clipId);
        Save();
        RaiseClipsChanged();
    }

    public void MoveClip(Guid clipId, int newIndex)
    {
        var ordered = _currentProfile.Clips.OrderBy(c => c.SortOrder).ToList();
        var clip = ordered.FirstOrDefault(c => c.Id == clipId);
        if (clip is null)
        {
            return;
        }

        ordered.Remove(clip);
        newIndex = Math.Clamp(newIndex, 0, ordered.Count);
        ordered.Insert(newIndex, clip);

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i;
        }

        Save();
        RaiseClipsChanged();
    }

    public void UpdateVolume(Guid clipId, double volume)
    {
        FindClip(clipId).Volume = Math.Clamp(volume, 0.0, 1.0);
        Save();
        RaiseClipsChanged();
    }

    public void UpdateHotkey(Guid clipId, HotkeyBinding? hotkey)
    {
        FindClip(clipId).Hotkey = hotkey;
        Save();
        RaiseClipsChanged();
    }

    public void RenameClip(Guid clipId, string name)
    {
        FindClip(clipId).Name = name;
        Save();
        RaiseClipsChanged();
    }

    public void Save() => _profileService.Save(_currentProfile);

    private SoundClip FindClip(Guid clipId) =>
        _currentProfile.Clips.FirstOrDefault(c => c.Id == clipId)
        ?? throw new InvalidOperationException($"Clip '{clipId}' not found in current profile.");

    private void RaiseClipsChanged() => ClipsChanged?.Invoke(this, EventArgs.Empty);
}
