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

    private AudioDeviceInfo? _selectedCableDevice;
    private AudioDeviceInfo? _selectedMonitorDevice;
    private AudioDeviceInfo? _selectedInputDevice;
    private string _stopAllHotkeyDisplay;

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> InputDevices { get; } = new();

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

            if (_suppressDeviceApply)
            {
                return;
            }

            _owner.SetCableDevice(value?.Id);
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

            if (_suppressDeviceApply)
            {
                return;
            }

            _owner.SetMonitorDevice(value?.Id);
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

    public string StopAllHotkeyDisplay
    {
        get => _stopAllHotkeyDisplay;
        private set => SetField(ref _stopAllHotkeyDisplay, value);
    }

    public RelayCommand AssignStopAllHotkeyCommand { get; }
    public RelayCommand ClearStopAllHotkeyCommand { get; }
    public RelayCommand OpenVbCablePageCommand { get; }

    public SettingsViewModel(MainViewModel owner)
    {
        _owner = owner;
        _stopAllHotkeyDisplay = owner.CurrentSettings.StopAllHotkey?.ToDisplayString() ?? "No hotkey";

        AssignStopAllHotkeyCommand = new RelayCommand(AssignStopAllHotkey);
        ClearStopAllHotkeyCommand = new RelayCommand(ClearStopAllHotkey);
        OpenVbCablePageCommand = new RelayCommand(OpenVbCablePage);

        RefreshDevices();
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

    private void OpenVbCablePage(object? parameter = null) => VbCableLauncher.OpenDownloadPage();
}
