using System.Windows;
using Clamor.App.Services;
using Clamor.App.ViewModels;
using Clamor.App.Views;
using Clamor.Audio;
using Clamor.Core.Models;
using Clamor.Core.Services;
using Clamor.Hotkeys;

namespace Clamor.App;

public partial class App : System.Windows.Application
{
    private AudioEngine? _audioEngine;
    private HotkeyService? _hotkeyService;
    private DeviceManager? _deviceManager;
    private MainViewModel? _mainViewModel;
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var profileService = new ProfileService();
        var settings = settingsService.Load();
        var clipLibrary = new ClipLibraryService(profileService, settings.ActiveProfileName);
        var fileDialogService = new FileDialogService();

        _deviceManager = new DeviceManager();
        _audioEngine = new AudioEngine();
        _hotkeyService = new HotkeyService();

        _mainViewModel = new MainViewModel(
            clipLibrary,
            settingsService,
            profileService,
            fileDialogService,
            _audioEngine,
            _hotkeyService,
            _deviceManager);

        _trayIconService = new TrayIconService();

        var mainWindow = new MainWindow(_mainViewModel, _trayIconService);
        MainWindow = mainWindow;

        _trayIconService.ShowRequested += (_, _) => mainWindow.RestoreFromTray();
        _trayIconService.StopAllRequested += (_, _) => _mainViewModel.StopAll();
        _trayIconService.ExitRequested += (_, _) => mainWindow.RequestExit();

        mainWindow.Show();

        CheckVbCable(mainWindow, settingsService, settings);
    }

    private void CheckVbCable(Window owner, ISettingsService settingsService, AppSettings settings)
    {
        if (settings.VbCablePromptDismissed || _deviceManager!.IsVbCableInstalled())
        {
            return;
        }

        if (VbCablePromptWindow.ShowPrompt(owner))
        {
            VbCableLauncher.OpenDownloadPage();
        }

        settings.VbCablePromptDismissed = true;
        settingsService.Save(settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _trayIconService?.Dispose();
        _audioEngine?.Dispose();
        _hotkeyService?.Dispose();
        _deviceManager?.Dispose();
        base.OnExit(e);
    }
}
