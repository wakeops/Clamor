using System.Collections.ObjectModel;
using System.Windows;
using Clamor.App.Services;
using Clamor.App.Views;
using Clamor.Audio;

namespace Clamor.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _owner;
    private bool _suppressDeviceApply;
    private bool _suppressProfileApply;

    private AudioDeviceInfo? _selectedCableDevice;
    private AudioDeviceInfo? _selectedMonitorDevice;
    private AudioDeviceInfo? _selectedInputDevice;
    private string? _selectedProfileName;
    private string _stopAllHotkeyDisplay;
    private string _newProfileName = string.Empty;

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> InputDevices { get; } = new();
    public ObservableCollection<string> ProfileNames { get; } = new();

    public bool IsVbCableInstalled => _owner.Devices.IsVbCableInstalled();

    public bool IsVbCableMissing => !IsVbCableInstalled;

    public bool MicPassthroughEnabled
    {
        get => _owner.MicPassthroughEnabled;
        set
        {
            _owner.MicPassthroughEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _owner.CurrentSettings.MinimizeToTrayOnClose;
        set
        {
            _owner.SetMinimizeToTray(value);
            OnPropertyChanged();
        }
    }

    public AudioDeviceInfo? SelectedCableDevice
    {
        get => _selectedCableDevice;
        set
        {
            if (!SetField(ref _selectedCableDevice, value))
            {
                return;
            }

            ApplyOutputDevices();
        }
    }

    public AudioDeviceInfo? SelectedMonitorDevice
    {
        get => _selectedMonitorDevice;
        set
        {
            if (!SetField(ref _selectedMonitorDevice, value))
            {
                return;
            }

            ApplyOutputDevices();
        }
    }

    public AudioDeviceInfo? SelectedInputDevice
    {
        get => _selectedInputDevice;
        set
        {
            if (!SetField(ref _selectedInputDevice, value))
            {
                return;
            }

            if (_suppressDeviceApply)
            {
                return;
            }

            _owner.SetInputDevice(value?.Id);
        }
    }

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (!SetField(ref _selectedProfileName, value))
            {
                return;
            }

            if (_suppressProfileApply || string.IsNullOrEmpty(value))
            {
                return;
            }

            _owner.SwitchProfile(value);
        }
    }

    public string StopAllHotkeyDisplay
    {
        get => _stopAllHotkeyDisplay;
        private set => SetField(ref _stopAllHotkeyDisplay, value);
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set => SetField(ref _newProfileName, value);
    }

    public RelayCommand AssignStopAllHotkeyCommand { get; }
    public RelayCommand ClearStopAllHotkeyCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand OpenVbCablePageCommand { get; }

    public SettingsViewModel(MainViewModel owner)
    {
        _owner = owner;
        _stopAllHotkeyDisplay = owner.CurrentSettings.StopAllHotkey?.ToDisplayString() ?? "No hotkey";

        AssignStopAllHotkeyCommand = new RelayCommand(AssignStopAllHotkey);
        ClearStopAllHotkeyCommand = new RelayCommand(ClearStopAllHotkey);
        CreateProfileCommand = new RelayCommand(CreateProfile, _ => !string.IsNullOrWhiteSpace(NewProfileName));
        DeleteProfileCommand = new RelayCommand(DeleteProfile, _ => SelectedProfileName is not null);
        OpenVbCablePageCommand = new RelayCommand(OpenVbCablePage);

        RefreshDevices();
        RefreshProfiles();
    }

    private void RefreshDevices()
    {
        _suppressDeviceApply = true;

        OutputDevices.Clear();
        foreach (var device in _owner.Devices.GetOutputDevices())
        {
            OutputDevices.Add(device);
        }

        InputDevices.Clear();
        foreach (var device in _owner.Devices.GetInputDevices())
        {
            InputDevices.Add(device);
        }

        var settings = _owner.CurrentSettings;
        _selectedCableDevice = OutputDevices.FirstOrDefault(d => d.Id == settings.OutputDeviceId);
        _selectedMonitorDevice = OutputDevices.FirstOrDefault(d => d.Id == settings.MonitorDeviceId);
        _selectedInputDevice = InputDevices.FirstOrDefault(d => d.Id == settings.InputDeviceId);
        OnPropertyChanged(nameof(SelectedCableDevice));
        OnPropertyChanged(nameof(SelectedMonitorDevice));
        OnPropertyChanged(nameof(SelectedInputDevice));
        OnPropertyChanged(nameof(IsVbCableInstalled));
        OnPropertyChanged(nameof(IsVbCableMissing));

        _suppressDeviceApply = false;
    }

    private void RefreshProfiles()
    {
        _suppressProfileApply = true;

        ProfileNames.Clear();
        foreach (var name in _owner.GetProfileNames())
        {
            ProfileNames.Add(name);
        }

        _selectedProfileName = _owner.ActiveProfileName;
        OnPropertyChanged(nameof(SelectedProfileName));

        _suppressProfileApply = false;
    }

    private void ApplyOutputDevices()
    {
        if (_suppressDeviceApply || SelectedCableDevice is null || SelectedMonitorDevice is null)
        {
            return;
        }

        _owner.SetOutputDevices(SelectedCableDevice.Id, SelectedMonitorDevice.Id);
    }

    private void AssignStopAllHotkey(object? parameter = null)
    {
        var binding = HotkeyCaptureDialog.Capture(Application.Current.MainWindow);
        if (binding is null)
        {
            return;
        }

        _owner.SetStopAllHotkey(binding);
        StopAllHotkeyDisplay = binding.ToDisplayString();
    }

    private void ClearStopAllHotkey(object? parameter = null)
    {
        _owner.SetStopAllHotkey(null);
        StopAllHotkeyDisplay = "No hotkey";
    }

    private void CreateProfile(object? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            return;
        }

        _owner.CreateProfile(NewProfileName.Trim());
        NewProfileName = string.Empty;
        RefreshProfiles();
    }

    private void DeleteProfile(object? parameter = null)
    {
        if (SelectedProfileName is null)
        {
            return;
        }

        try
        {
            _owner.DeleteProfile(SelectedProfileName);
        }
        catch (InvalidOperationException ex)
        {
            MessageDialog.ShowInfo(Application.Current.MainWindow, "Can't delete profile", ex.Message);
            return;
        }

        RefreshProfiles();
    }

    private void OpenVbCablePage(object? parameter = null) => VbCableLauncher.OpenDownloadPage();
}
