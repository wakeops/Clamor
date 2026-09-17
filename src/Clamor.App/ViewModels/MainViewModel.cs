using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
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
    /// <summary>Always listed first in the deck dropdown.</summary>
    private const string DefaultProfileName = "Default";

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
    private double _masterVolume;

    /// <summary>True while the hotkey-capture dialog is up. <see cref="OnHotkeyPressed"/> checks
    /// this before acting — global hotkeys stay registered (and can still be re-pressed to
    /// confirm the same combo) while the dialog is open, but they shouldn't also play/stop clips
    /// out from under the user while they're just trying to (re)bind a key. <c>volatile</c>
    /// because it's set on the UI thread but read from <see cref="HotkeyService"/>'s own thread.</summary>
    private volatile bool _isCapturingHotkey;
    private string _searchText = string.Empty;

    public ObservableCollection<SoundClipViewModel> Clips { get; } = new();

    /// <summary>Filtered view of <see cref="Clips"/> that the sound grid binds to — keeps the
    /// search box from having to mutate the underlying collection.</summary>
    public ICollectionView ClipsView { get; }

    public ObservableCollection<string> ProfileNames { get; } = new();

    /// <summary>Left nav rail entries. Just "Decks" for now, always active — no other
    /// destination exists yet to navigate away to.</summary>
    public ObservableCollection<NavItemViewModel> NavItems { get; } =
        new() { new NavItemViewModel("DECKS", "\uED25", isActive: true) };

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

    /// <summary>Linear gain (0.0-1.0) applied to both the cable and monitor output buses.</summary>
    public double MasterVolume
    {
        get => _masterVolume;
        set
        {
            if (!SetField(ref _masterVolume, value))
            {
                return;
            }

            _audioEngine.MasterVolume = (float)value;
            _settings.MasterVolume = value;
            _settingsService.Save(_settings);
        }
    }

    /// <summary>Filters the sound grid by clip name (case-insensitive substring match).</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
            {
                return;
            }

            ClipsView.Refresh();
        }
    }

    /// <summary>Count of clips currently visible in the grid — shrinks as a search filter narrows
    /// the results, matching <see cref="ClipsView"/> rather than the unfiltered <see cref="Clips"/>.</summary>
    public int SoundCount => ClipsView.Cast<object>().Count();

    /// <summary>"1 Sound" / "N Sounds" — singular only at exactly one clip.</summary>
    public string SoundCountLabel => SoundCount == 1 ? "1 Sound" : $"{SoundCount} Sounds";

    public string ActiveProfileName => _clipLibrary.CurrentProfile.Name;

    /// <summary>Two-way bound to the deck dropdown — picking a profile here both loads its
    /// sounds/hotkeys into the grid and makes it the active (live-hotkey) profile.</summary>
    public string SelectedProfileName
    {
        get => ActiveProfileName;
        set => SwitchProfile(value);
    }

    public RelayCommand AddClipCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand<SoundClipViewModel> PlayClipCommand { get; }
    public RelayCommand<SoundClipViewModel> RemoveClipCommand { get; }
    public RelayCommand<SoundClipViewModel> RenameClipCommand { get; }
    public RelayCommand<SoundClipViewModel> AssignHotkeyCommand { get; }
    public RelayCommand<SoundClipViewModel> ClearHotkeyCommand { get; }
    public RelayCommand AddProfileCommand { get; }

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

        ClipsView = CollectionViewSource.GetDefaultView(Clips);
        ClipsView.Filter = MatchesSearch;
        ((INotifyCollectionChanged)ClipsView).CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SoundCount));
            OnPropertyChanged(nameof(SoundCountLabel));
        };

        _settings = _settingsService.Load();
        _micPassthroughEnabled = _settings.MicPassthroughEnabled;
        _masterVolume = _settings.MasterVolume;
        _audioEngine.MasterVolume = (float)_masterVolume;

        _audioEngine.ClipStopped += OnClipStopped;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        AddClipCommand = new RelayCommand(AddClips);
        StopAllCommand = new RelayCommand(StopAll);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        PlayClipCommand = new RelayCommand<SoundClipViewModel>(PlayClip);
        RemoveClipCommand = new RelayCommand<SoundClipViewModel>(RemoveClip);
        RenameClipCommand = new RelayCommand<SoundClipViewModel>(BeginRenameClip);
        AssignHotkeyCommand = new RelayCommand<SoundClipViewModel>(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand<SoundClipViewModel>(ClearHotkey);
        AddProfileCommand = new RelayCommand(AddProfile);

        LoadClipsFromProfile();
        RefreshProfileNames();
        EnsureDefaultDevicesConfigured();
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

    private bool MatchesSearch(object item) =>
        string.IsNullOrWhiteSpace(SearchText) ||
        item is not SoundClipViewModel clip ||
        clip.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

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

    private void BeginRenameClip(SoundClipViewModel? clipVm)
    {
        if (clipVm is null)
        {
            return;
        }

        var newName = InputDialog.Prompt(Application.Current.MainWindow, "Rename Clip", "Clip name:", clipVm.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        RenameClip(clipVm, newName.Trim());
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
        if (_isCapturingHotkey)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (_isCapturingHotkey)
            {
                return;
            }

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

        // Free the clip's own current combo so pressing it again during capture reaches the
        // dialog as a normal keystroke instead of being swallowed as an already-registered
        // global hotkey — otherwise re-confirming the same binding silently does nothing.
        UnregisterClipHotkey(clipVm);
        _isCapturingHotkey = true;
        HotkeyBinding? binding;
        try
        {
            binding = HotkeyCaptureDialog.Capture(Application.Current.MainWindow);
        }
        finally
        {
            _isCapturingHotkey = false;
        }

        if (binding is null)
        {
            TryRegisterClipHotkey(clipVm); // cancelled — restore the binding we freed above
            return;
        }

        try
        {
            var id = _hotkeyService.Register(binding);
            _hotkeyIdToClip[id] = clipVm.Id;
            clipVm.HotkeyRegistrationId = id;
        }
        catch (InvalidOperationException ex)
        {
            MessageDialog.ShowInfo(Application.Current.MainWindow, "Hotkey unavailable", ex.Message);
            TryRegisterClipHotkey(clipVm); // new combo was rejected — restore the old one
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

    /// <summary>
    /// Fills in any device settings.json has never had a value for — cable output, monitor
    /// output, and mic input — using the system's current defaults, so the app plays and
    /// listens through something out of the box rather than staying silent until a first visit
    /// to Settings. Leaves anything already configured (even a since-unplugged device) alone.
    /// </summary>
    private void EnsureDefaultDevicesConfigured()
    {
        var changed = false;

        if (string.IsNullOrEmpty(_settings.MonitorDeviceId))
        {
            var defaultMonitor = _deviceManager.GetOutputDevices().FirstOrDefault(d => d.IsDefault);
            if (defaultMonitor is not null)
            {
                _settings.MonitorDeviceId = defaultMonitor.Id;
                changed = true;
            }
        }

        if (string.IsNullOrEmpty(_settings.OutputDeviceId))
        {
            var outputDevices = _deviceManager.GetOutputDevices();
            var vbCable = outputDevices.FirstOrDefault(d =>
                d.Name.Contains(DeviceManager.VbCableDeviceNameFragment, StringComparison.OrdinalIgnoreCase));
            var cableFallback = vbCable ?? outputDevices.FirstOrDefault(d => d.IsDefault);
            if (cableFallback is not null)
            {
                _settings.OutputDeviceId = cableFallback.Id;
                changed = true;
            }
        }

        if (string.IsNullOrEmpty(_settings.InputDeviceId))
        {
            var defaultInput = _deviceManager.GetInputDevices().FirstOrDefault(d => d.IsDefault);
            if (defaultInput is not null)
            {
                _settings.InputDeviceId = defaultInput.Id;
                changed = true;
            }
        }

        if (changed)
        {
            _settingsService.Save(_settings);
        }
    }

    private void ApplyOutputDevices()
    {
        if (string.IsNullOrEmpty(_settings.OutputDeviceId) || string.IsNullOrEmpty(_settings.MonitorDeviceId))
        {
            return;
        }

        _audioEngine.StartOutputs(_settings.OutputDeviceId, _settings.MonitorDeviceId);
    }

    /// <summary>
    /// Cable and monitor devices are saved independently the moment either changes — picking
    /// one shouldn't be silently dropped just because the other hasn't been chosen yet. The
    /// engine only actually starts once both are set (<see cref="ApplyOutputDevices"/>).
    /// </summary>
    public void SetCableDevice(string? cableDeviceId)
    {
        _settings.OutputDeviceId = cableDeviceId;
        _settingsService.Save(_settings);
        ApplyOutputDevices();
    }

    public void SetMonitorDevice(string? monitorDeviceId)
    {
        _settings.MonitorDeviceId = monitorDeviceId;
        _settingsService.Save(_settings);
        ApplyOutputDevices();
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

    /// <summary>Loads a different profile's sounds/hotkeys into the grid and makes it the one
    /// whose hotkeys fire globally.</summary>
    public void SwitchProfile(string? name)
    {
        if (string.IsNullOrEmpty(name) || string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
        OnPropertyChanged(nameof(SelectedProfileName));
    }

    public void SetMinimizeToTray(bool value)
    {
        _settings.MinimizeToTrayOnClose = value;
        _settingsService.Save(_settings);
    }

    public void CreateProfile(string name)
    {
        _profileService.LoadOrCreate(name);
        RefreshProfileNames();
        SwitchProfile(name);
    }

    /// <summary>All profile names with "Default" listed first, the rest alphabetically after.</summary>
    private void RefreshProfileNames()
    {
        var all = _profileService.GetProfileNames();
        var isDefault = new Func<string, bool>(n => string.Equals(n, DefaultProfileName, StringComparison.OrdinalIgnoreCase));

        ProfileNames.Clear();
        if (all.Any(isDefault))
        {
            ProfileNames.Add(DefaultProfileName);
        }

        foreach (var name in all.Where(n => !isDefault(n)))
        {
            ProfileNames.Add(name);
        }
    }

    private void AddProfile(object? parameter = null)
    {
        var name = InputDialog.Prompt(Application.Current.MainWindow, "New Profile", "Profile name:");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        CreateProfile(name.Trim());
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
