using Clamor.Core.Models;

namespace Clamor.App.ViewModels;

/// <summary>
/// UI-facing wrapper around a <see cref="SoundClip"/>. Owned and mutated by
/// <see cref="MainViewModel"/>, which is the single point of coordination between the sound
/// grid, the audio engine, and the hotkey service.
/// </summary>
public sealed class SoundClipViewModel : ViewModelBase
{
    private readonly SoundClip _model;
    private readonly Action<Guid, double>? _onVolumeChanged;
    private string _hotkeyDisplay;
    private double _volume;
    private int _activeVoiceCount;

    public SoundClipViewModel(SoundClip model, Action<Guid, double>? onVolumeChanged = null)
    {
        _model = model;
        _onVolumeChanged = onVolumeChanged;
        _volume = model.Volume;
        _hotkeyDisplay = model.Hotkey?.ToDisplayString() ?? "No hotkey";
    }

    public Guid Id => _model.Id;

    public string FilePath => _model.FilePath;

    public int SortOrder => _model.SortOrder;

    public string Name
    {
        get => _model.Name;
        set
        {
            if (_model.Name == value)
            {
                return;
            }

            _model.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Linear gain, 0.0–1.0. Setting it (e.g. from the volume slider's two-way binding)
    /// persists through the callback given at construction, so the profile stays in sync.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (SetField(ref _volume, value))
            {
                _onVolumeChanged?.Invoke(Id, value);
            }
        }
    }

    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        private set => SetField(ref _hotkeyDisplay, value);
    }

    public HotkeyBinding? Hotkey => _model.Hotkey;

    public bool HasHotkey => _model.Hotkey is not null;

    /// <summary>Registration id handed back by HotkeyService for the currently-bound hotkey,
    /// or null if this clip has no hotkey or it failed to register. Purely runtime bookkeeping —
    /// never persisted.</summary>
    public int? HotkeyRegistrationId { get; set; }

    public bool IsPlaying => _activeVoiceCount > 0;

    internal void IncrementPlaying()
    {
        _activeVoiceCount++;
        OnPropertyChanged(nameof(IsPlaying));
    }

    internal void DecrementPlaying()
    {
        if (_activeVoiceCount > 0)
        {
            _activeVoiceCount--;
        }

        OnPropertyChanged(nameof(IsPlaying));
    }

    internal void RefreshHotkeyDisplay(HotkeyBinding? binding)
    {
        HotkeyDisplay = binding?.ToDisplayString() ?? "No hotkey";
        OnPropertyChanged(nameof(Hotkey));
        OnPropertyChanged(nameof(HasHotkey));
    }
}
