using System.ComponentModel;
using System.Windows;
using Clamor.App.Services;
using Clamor.App.ViewModels;

namespace Clamor.App.Views;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions = { ".mp3", ".wav" };

    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private bool _isExiting;

    public MainWindow(MainViewModel viewModel, TrayIconService trayIconService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _trayIconService = trayIconService;
        DataContext = viewModel;

        Closing += OnClosing;
        Closed += OnClosed;
        Drop += OnDrop;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting || !_viewModel.CurrentSettings.MinimizeToTrayOnClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _trayIconService.Show();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        var audioFiles = paths.Where(p => SupportedExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));
        _viewModel.AddClipsFromPaths(audioFiles);
    }

    public void RequestExit()
    {
        _isExiting = true;
        System.Windows.Application.Current.Shutdown();
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _trayIconService.Hide();
    }
}
