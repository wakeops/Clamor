using System.Collections.ObjectModel;
using System.IO;
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
    /// <summary>Always pinned first in the profile tab bar and never reorderable.</summary>
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

    /// <summary>Hotkey registrations for the ACTIVE profile's clips only (hotkeyId -> clipId).
    /// Independent of <see cref="Clips"/>, which shows whichever profile is being viewed —
    /// the active profile's hotkeys stay live even while browsing a different one.</summary>
    private readonly Dictionary<int, Guid> _activeHotkeyMap = new();

    private AppSettings _settings;
    private Profile _activeProfile;
    private int? _stopAllHotkeyId;
    private bool _micPassthroughEnabled;

    public ObservableCollection<SoundClipViewModel> Clips { get; } = new();

    public ObservableCollection<ProfileTabViewModel> ProfileTabs { get; } = new();

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

    /// <summary>The profile whose hotkeys are currently live, regardless of what's being viewed.</summary>
    public string ActiveProfileName => _activeProfile.Name;

    /// <summary>The profile currently shown in the sound grid — may or may not be the active one.</summary>
    public string ViewedProfileName => _clipLibrary.CurrentProfile.Name;

    public bool IsViewingActiveProfile =>
        string.Equals(ViewedProfileName, ActiveProfileName, StringComparison.OrdinalIgnoreCase);

    public bool IsViewingInactiveProfile => !IsViewingActiveProfile;

    public RelayCommand AddClipCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand<SoundClipViewModel> PlayClipCommand { get; }
    public RelayCommand<SoundClipViewModel> RemoveClipCommand { get; }
    public RelayCommand<SoundClipViewModel> AssignHotkeyCommand { get; }
    public RelayCommand<SoundClipViewModel> ClearHotkeyCommand { get; }
    public RelayCommand<string> SelectProfileCommand { get; }
    public RelayCommand ActivateViewedProfileCommand { get; }
    public RelayCommand AddProfileCommand { get; }
    public RelayCommand DeleteViewedProfileCommand { get; }

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
        _activeProfile = _clipLibrary.CurrentProfile;

        _audioEngine.ClipStopped += OnClipStopped;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        AddClipCommand = new RelayCommand(AddClips);
        StopAllCommand = new RelayCommand(StopAll);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        PlayClipCommand = new RelayCommand<SoundClipViewModel>(PlayClip);
        RemoveClipCommand = new RelayCommand<SoundClipViewModel>(RemoveClip);
        AssignHotkeyCommand = new RelayCommand<SoundClipViewModel>(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand<SoundClipViewModel>(ClearHotkey);
        SelectProfileCommand = new RelayCommand<string>(SelectProfile);
        ActivateViewedProfileCommand = new RelayCommand(_ => ActivateProfile(ViewedProfileName), _ => IsViewingInactiveProfile);
        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteViewedProfileCommand = new RelayCommand(_ => DeleteViewedProfile(), _ => IsViewingInactiveProfile);

        LoadClipsFromProfile();
        RegisterActiveProfileHotkeys();
        RefreshProfileTabs();
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
        }

        SyncActiveHotkeyDisplays();
    }

    /// <summary>Reflects already-registered active-profile hotkeys onto the freshly created
    /// view models for this load (a no-op unless the viewed profile is also the active one).</summary>
    private void SyncActiveHotkeyDisplays()
    {
        foreach (var (hotkeyId, clipId) in _activeHotkeyMap)
        {
            if (_clipsById.TryGetValue(clipId, out var vm))
            {
                vm.HotkeyRegistrationId = hotkeyId;
            }
        }
    }

    /// <summary>(Re)registers global hotkeys for the active profile's clips, independent of
    /// whatever profile is currently displayed in <see cref="Clips"/>.</summary>
    private void RegisterActiveProfileHotkeys()
    {
        foreach (var hotkeyId in _activeHotkeyMap.Keys.ToList())
        {
            _hotkeyService.Unregister(hotkeyId);
        }

        _activeHotkeyMap.Clear();

        foreach (var vm in Clips)
        {
            vm.HotkeyRegistrationId = null;
        }

        foreach (var clip in _activeProfile.Clips)
        {
            if (clip.Hotkey is null)
            {
                continue;
            }

            try
            {
                var id = _hotkeyService.Register(clip.Hotkey);
                _activeHotkeyMap[id] = clip.Id;
                if (_clipsById.TryGetValue(clip.Id, out var vm))
                {
                    vm.HotkeyRegistrationId = id;
                }
            }
            catch (InvalidOperationException)
            {
                // Saved hotkey is claimed by something else this session — the clip keeps its
                // saved binding (still shown in the UI) but simply won't fire until reassigned.
            }
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

            if (!_activeHotkeyMap.TryGetValue(hotkeyId, out var clipId))
            {
                return;
            }

            if (_clipsById.TryGetValue(clipId, out var clipVm))
            {
                PlayClip(clipVm);
                return;
            }

            // Active profile's clip isn't the one currently on screen — play it directly,
            // with no view model around to reflect a "now playing" glow.
            var clip = _activeProfile.Clips.FirstOrDefault(c => c.Id == clipId);
            if (clip is not null)
            {
                _audioEngine.PlayClip(clip.FilePath, clip.Volume);
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

        // Only the active profile's hotkeys are ever globally registered — editing one on a
        // profile you're merely viewing just saves it for when that profile is made active.
        if (IsViewingActiveProfile)
        {
            UnregisterClipHotkey(clipVm);

            try
            {
                var id = _hotkeyService.Register(binding);
                _activeHotkeyMap[id] = clipVm.Id;
                clipVm.HotkeyRegistrationId = id;
            }
            catch (InvalidOperationException ex)
            {
                MessageDialog.ShowInfo(Application.Current.MainWindow, "Hotkey unavailable", ex.Message);
                return;
            }
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

        if (IsViewingActiveProfile)
        {
            UnregisterClipHotkey(clipVm);
        }

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
        _activeHotkeyMap.Remove(id);
        clipVm.HotkeyRegistrationId = null;
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

    /// <summary>Shows a different profile's sounds/hotkeys in the grid without changing which
    /// profile is active — lets a profile be browsed or edited ahead of being switched to.</summary>
    public void SelectProfile(string? name)
    {
        if (string.IsNullOrEmpty(name) || string.Equals(name, ViewedProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _clipLibrary.LoadProfile(name);
        LoadClipsFromProfile();
        RefreshProfileTabs();
        OnPropertyChanged(nameof(ViewedProfileName));
        OnPropertyChanged(nameof(IsViewingActiveProfile));
        OnPropertyChanged(nameof(IsViewingInactiveProfile));
    }

    /// <summary>Makes the given profile the one whose hotkeys fire globally, regardless of
    /// which profile is currently being viewed.</summary>
    public void ActivateProfile(string name)
    {
        if (string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopAll();

        _activeProfile = string.Equals(name, ViewedProfileName, StringComparison.OrdinalIgnoreCase)
            ? _clipLibrary.CurrentProfile
            : _profileService.LoadOrCreate(name);

        _settings.ActiveProfileName = name;
        _settingsService.Save(_settings);

        RegisterActiveProfileHotkeys();
        RefreshProfileTabs();

        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(IsViewingActiveProfile));
        OnPropertyChanged(nameof(IsViewingInactiveProfile));
    }

    public void SetMinimizeToTray(bool value)
    {
        _settings.MinimizeToTrayOnClose = value;
        _settingsService.Save(_settings);
    }

    public void CreateProfile(string name)
    {
        _profileService.LoadOrCreate(name);

        if (!string.Equals(name, DefaultProfileName, StringComparison.OrdinalIgnoreCase) &&
            !_settings.ProfileOrder.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            _settings.ProfileOrder.Add(name);
            _settingsService.Save(_settings);
        }

        SelectProfile(name);
        RefreshProfileTabs();
    }

    public void DeleteProfile(string name)
    {
        if (string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the active profile — switch to another one first.");
        }

        var wasViewed = string.Equals(name, ViewedProfileName, StringComparison.OrdinalIgnoreCase);
        _profileService.Delete(name);

        _settings.ProfileOrder.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(_settings);

        if (wasViewed)
        {
            SelectProfile(ActiveProfileName);
        }

        RefreshProfileTabs();
    }

    public void RenameActiveProfile(string newName)
    {
        var oldName = ActiveProfileName;
        var wasViewed = IsViewingActiveProfile;

        _profileService.Rename(oldName, newName);
        _activeProfile = _profileService.Load(newName);
        _settings.ActiveProfileName = newName;

        var orderIndex = _settings.ProfileOrder.FindIndex(n => string.Equals(n, oldName, StringComparison.OrdinalIgnoreCase));
        if (orderIndex >= 0)
        {
            _settings.ProfileOrder[orderIndex] = newName;
        }

        _settingsService.Save(_settings);

        if (wasViewed)
        {
            _clipLibrary.LoadProfile(newName);
            LoadClipsFromProfile();
        }

        RegisterActiveProfileHotkeys();
        RefreshProfileTabs();

        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ViewedProfileName));
        OnPropertyChanged(nameof(IsViewingActiveProfile));
        OnPropertyChanged(nameof(IsViewingInactiveProfile));
    }

    /// <summary>Moves a dragged (non-Default) profile so it sits immediately before/after
    /// <paramref name="targetName"/> in the tab bar. Dropping onto "Default" moves it to the
    /// front of the non-Default group, since Default itself can never be displaced.</summary>
    public void ReorderProfile(string draggedName, string targetName)
    {
        if (string.Equals(draggedName, DefaultProfileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(draggedName, targetName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var order = GetOrderedProfileNames()
            .Where(n => !string.Equals(n, DefaultProfileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!order.Remove(draggedName))
        {
            return;
        }

        if (string.Equals(targetName, DefaultProfileName, StringComparison.OrdinalIgnoreCase))
        {
            order.Insert(0, draggedName);
        }
        else
        {
            var targetIndex = order.FindIndex(n => string.Equals(n, targetName, StringComparison.OrdinalIgnoreCase));
            order.Insert(targetIndex < 0 ? order.Count : targetIndex, draggedName);
        }

        _settings.ProfileOrder = order;
        _settingsService.Save(_settings);
        RefreshProfileTabs();
    }

    /// <summary>All profile names with "Default" pinned first, followed by the rest in the
    /// user's saved order (<see cref="AppSettings.ProfileOrder"/>) — any profile not yet in
    /// that list, such as one just created, sorts alphabetically after the ones that are.</summary>
    private IEnumerable<string> GetOrderedProfileNames()
    {
        var all = _profileService.GetProfileNames();
        var isDefault = new Func<string, bool>(n => string.Equals(n, DefaultProfileName, StringComparison.OrdinalIgnoreCase));
        var remaining = all.Where(n => !isDefault(n)).ToList();

        var ordered = new List<string>();
        foreach (var name in _settings.ProfileOrder)
        {
            var match = remaining.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
                remaining.Remove(match);
            }
        }

        ordered.AddRange(remaining);

        if (all.Any(isDefault))
        {
            yield return DefaultProfileName;
        }

        foreach (var name in ordered)
        {
            yield return name;
        }
    }

    private void RefreshProfileTabs()
    {
        ProfileTabs.Clear();
        foreach (var name in GetOrderedProfileNames())
        {
            ProfileTabs.Add(new ProfileTabViewModel(name)
            {
                IsActive = string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase),
                IsSelected = string.Equals(name, ViewedProfileName, StringComparison.OrdinalIgnoreCase),
            });
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

    private void DeleteViewedProfile()
    {
        var name = ViewedProfileName;
        var confirmed = MessageDialog.ShowConfirm(
            Application.Current.MainWindow,
            "Delete Profile",
            $"Delete the profile \"{name}\"? This can't be undone.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            DeleteProfile(name);
        }
        catch (InvalidOperationException ex)
        {
            MessageDialog.ShowInfo(Application.Current.MainWindow, "Can't delete profile", ex.Message);
        }
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
