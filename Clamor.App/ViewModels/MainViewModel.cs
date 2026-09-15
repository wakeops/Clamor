using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Clamor.App.Services;
using Clamor.App.Views;
using Clamor.Audio;
using Clamor.Core.Models;
using Clamor.Core.Services;
using Clamor.Hotkeys;

namespace Clamor.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IClipLibraryService _clipLibrary;
    private readonly ISettingsService _settingsService;
    private readonly IProfileService _profileService;
    private readonly IFileDialogService _fileDialogService;
    private readonly AudioEngine _audioEngine;
    private readonly HotkeyService _hotkeyService;
    private readonly DeviceManager _deviceManager;
    private readonly Dispatcher _dispatcher;

    private readonly Dictionary<Guid, SoundClipViewModel> _clipsById = new();
    private readonly Dictionary<Guid, Guid> _voiceToClip = new();
    private readonly object _voiceMapLock = new();
    private readonly Dictionary<int, Guid> _hotkeyIdToClip = new();

    private AppSettings _settings;
    private int? _stopAllHotkeyId;
    private bool _micPassthroughEnabled;

    public ObservableCollection<SoundClipViewModel> Clips { get; } = new();

    public bool MicPassthroughEnabled
    {
        get => _micPassthroughEnabled;
        set
        {
            if (!SetField(ref _micPassthroughEnabled, value))
            {
                return;
            }

            _settings.MicPassthroughEnabled = value;
            _settingsService.Save(_settings);
            ApplyMicPassthroughState();
        }
    }

    public string ActiveProfileName => _clipLibrary.CurrentProfile.Name;

    public RelayCommand AddClipCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand<SoundClipViewModel> PlayClipCommand { get; }
    public RelayCommand<SoundClipViewModel> RemoveClipCommand { get; }
    public RelayCommand<SoundClipViewModel> AssignHotkeyCommand { get; }
    public RelayCommand<SoundClipViewModel> ClearHotkeyCommand { get; }

    public MainViewModel(
        IClipLibraryService clipLibrary,
        ISettingsService settingsService,
        IProfileService profileService,
        IFileDialogService fileDialogService,
        AudioEngine audioEngine,
        HotkeyService hotkeyService,
        DeviceManager deviceManager)
    {
        _clipLibrary = clipLibrary;
        _settingsService = settingsService;
        _profileService = profileService;
        _fileDialogService = fileDialogService;
        _audioEngine = audioEngine;
        _hotkeyService = hotkeyService;
        _deviceManager = deviceManager;
        _dispatcher = Application.Current.Dispatcher;

        _settings = _settingsService.Load();
        _micPassthroughEnabled = _settings.MicPassthroughEnabled;

        _audioEngine.ClipStopped += OnClipStopped;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        AddClipCommand = new RelayCommand(AddClips);
        StopAllCommand = new RelayCommand(StopAll);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        PlayClipCommand = new RelayCommand<SoundClipViewModel>(PlayClip);
        RemoveClipCommand = new RelayCommand<SoundClipViewModel>(RemoveClip);
        AssignHotkeyCommand = new RelayCommand<SoundClipViewModel>(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand<SoundClipViewModel>(ClearHotkey);

        LoadClipsFromProfile();
        ApplyOutputDevices();
        ApplyMicPassthroughState();
        RegisterStopAllHotkey();
    }

    private void LoadClipsFromProfile()
    {
        Clips.Clear();
        _clipsById.Clear();

        foreach (var clip in _clipLibrary.CurrentProfile.Clips.OrderBy(c => c.SortOrder))
        {
            var vm = new SoundClipViewModel(clip, OnClipVolumeChanged);
            Clips.Add(vm);
            _clipsById[vm.Id] = vm;
            TryRegisterClipHotkey(vm);
        }
    }

    public void AddClips(object? parameter = null)
    {
        var paths = parameter as IEnumerable<string> ?? _fileDialogService.OpenAudioFiles();
        AddClipsFromPaths(paths);
    }

    public void AddClipsFromPaths(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            SoundClip clip;
            try
            {
                clip = _clipLibrary.AddClip(path);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            var vm = new SoundClipViewModel(clip, OnClipVolumeChanged);
            Clips.Add(vm);
            _clipsById[vm.Id] = vm;
        }
    }

    private void OnClipVolumeChanged(Guid clipId, double volume) => _clipLibrary.UpdateVolume(clipId, volume);

    public void RemoveClip(SoundClipViewModel? clipVm)
    {
        if (clipVm is null)
        {
            return;
        }

        UnregisterClipHotkey(clipVm);
        _clipLibrary.RemoveClip(clipVm.Id);
        Clips.Remove(clipVm);
        _clipsById.Remove(clipVm.Id);
    }

    public void RenameClip(SoundClipViewModel clipVm, string name)
    {
        clipVm.Name = name;
        _clipLibrary.RenameClip(clipVm.Id, name);
    }

    public void PlayClip(SoundClipViewModel? clipVm)
    {
        if (clipVm is null)
        {
            return;
        }

        var voiceId = _audioEngine.PlayClip(clipVm.FilePath, clipVm.Volume);
        lock (_voiceMapLock)
        {
            _voiceToClip[voiceId] = clipVm.Id;
        }

        clipVm.IncrementPlaying();
    }

    public void StopAll(object? parameter = null) => _audioEngine.StopAll();

    private void OnClipStopped(object? sender, Guid voiceId)
    {
        Guid clipId;
        lock (_voiceMapLock)
        {
            if (!_voiceToClip.Remove(voiceId, out clipId))
            {
                return;
            }
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (_clipsById.TryGetValue(clipId, out var clipVm))
            {
                clipVm.DecrementPlaying();
            }
        });
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_stopAllHotkeyId is not null && hotkeyId == _stopAllHotkeyId)
            {
                StopAll();
                return;
            }

            if (_hotkeyIdToClip.TryGetValue(hotkeyId, out var clipId) &&
                _clipsById.TryGetValue(clipId, out var clipVm))
            {
                PlayClip(clipVm);
            }
        });
    }

    private void BeginHotkeyCapture(SoundClipViewModel? clipVm)
    {
        if (clipVm is null)
        {
            return;
        }

        var binding = HotkeyCaptureDialog.Capture(Application.Current.MainWindow);
        if (binding is null)
        {
            return;
        }

        UnregisterClipHotkey(clipVm);

        try
        {
            var id = _hotkeyService.Register(binding);
            _hotkeyIdToClip[id] = clipVm.Id;
            clipVm.HotkeyRegistrationId = id;
        }
        catch (InvalidOperationException ex)
        {
            MessageDialog.ShowInfo(Application.Current.MainWindow, "Hotkey unavailable", ex.Message);
            return;
        }

        _clipLibrary.UpdateHotkey(clipVm.Id, binding);
        clipVm.RefreshHotkeyDisplay(binding);
    }

    private void ClearHotkey(SoundClipViewModel? clipVm)
    {
        if (clipVm is null)
        {
            return;
        }

        UnregisterClipHotkey(clipVm);
        _clipLibrary.UpdateHotkey(clipVm.Id, null);
        clipVm.RefreshHotkeyDisplay(null);
    }

    private void UnregisterClipHotkey(SoundClipViewModel clipVm)
    {
        if (clipVm.HotkeyRegistrationId is not int id)
        {
            return;
        }

        _hotkeyService.Unregister(id);
        _hotkeyIdToClip.Remove(id);
        clipVm.HotkeyRegistrationId = null;
    }

    private void TryRegisterClipHotkey(SoundClipViewModel clipVm)
    {
        if (clipVm.Hotkey is null)
        {
            return;
        }

        try
        {
            var id = _hotkeyService.Register(clipVm.Hotkey);
            _hotkeyIdToClip[id] = clipVm.Id;
            clipVm.HotkeyRegistrationId = id;
        }
        catch (InvalidOperationException)
        {
            // Saved hotkey is claimed by something else this session — the clip keeps its
            // saved binding (still shown in the UI) but simply won't fire until reassigned.
        }
    }

    private void RegisterStopAllHotkey()
    {
        if (_stopAllHotkeyId is int existing)
        {
            _hotkeyService.Unregister(existing);
            _stopAllHotkeyId = null;
        }

        if (_settings.StopAllHotkey is null)
        {
            return;
        }

        try
        {
            _stopAllHotkeyId = _hotkeyService.Register(_settings.StopAllHotkey);
        }
        catch (InvalidOperationException)
        {
            _stopAllHotkeyId = null;
        }
    }

    public void SetStopAllHotkey(HotkeyBinding? binding)
    {
        _settings.StopAllHotkey = binding;
        _settingsService.Save(_settings);
        RegisterStopAllHotkey();
    }

    private void ApplyOutputDevices()
    {
        if (string.IsNullOrEmpty(_settings.OutputDeviceId) || string.IsNullOrEmpty(_settings.MonitorDeviceId))
        {
            return;
        }

        _audioEngine.StartOutputs(_settings.OutputDeviceId, _settings.MonitorDeviceId);
    }

    public void SetOutputDevices(string cableDeviceId, string monitorDeviceId)
    {
        _settings.OutputDeviceId = cableDeviceId;
        _settings.MonitorDeviceId = monitorDeviceId;
        _settingsService.Save(_settings);
        _audioEngine.StartOutputs(cableDeviceId, monitorDeviceId);
    }

    public void SetInputDevice(string? inputDeviceId)
    {
        _settings.InputDeviceId = inputDeviceId;
        _settingsService.Save(_settings);
        ApplyMicPassthroughState();
    }

    private void ApplyMicPassthroughState()
    {
        if (_micPassthroughEnabled && !string.IsNullOrEmpty(_settings.InputDeviceId))
        {
            _audioEngine.StartMicPassthrough(_settings.InputDeviceId);
        }
        else
        {
            _audioEngine.StopMicPassthrough();
        }
    }

    public AppSettings CurrentSettings => _settings;

    public DeviceManager Devices => _deviceManager;

    public void SwitchProfile(string name)
    {
        StopAll();
        foreach (var clipVm in Clips)
        {
            UnregisterClipHotkey(clipVm);
        }

        _clipLibrary.LoadProfile(name);
        _settings.ActiveProfileName = name;
        _settingsService.Save(_settings);
        LoadClipsFromProfile();
        OnPropertyChanged(nameof(ActiveProfileName));
    }

    public void SetMinimizeToTray(bool value)
    {
        _settings.MinimizeToTrayOnClose = value;
        _settingsService.Save(_settings);
    }

    public IReadOnlyList<string> GetProfileNames() => _profileService.GetProfileNames();

    public void CreateProfile(string name)
    {
        _profileService.LoadOrCreate(name);
        SwitchProfile(name);
    }

    public void DeleteProfile(string name)
    {
        if (string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the active profile — switch to another one first.");
        }

        _profileService.Delete(name);
    }

    public void RenameActiveProfile(string newName)
    {
        var oldName = ActiveProfileName;
        _profileService.Rename(oldName, newName);
        SwitchProfile(newName);
    }

    private void OpenSettings(object? parameter = null)
    {
        var settingsViewModel = new SettingsViewModel(this);
        var window = new SettingsWindow(settingsViewModel) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    public void Dispose()
    {
        _audioEngine.ClipStopped -= OnClipStopped;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
    }
}
