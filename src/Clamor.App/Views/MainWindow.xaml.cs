using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clamor.App.Services;
using Clamor.App.ViewModels;

namespace Clamor.App.Views;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions = { ".mp3", ".wav" };

    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private bool _isExiting;
    private System.Windows.Point _profileDragStart;
    private bool _profileDragCandidate;

    public MainWindow(MainViewModel viewModel, TrayIconService trayIconService)
    {
        InitializeComponent();
        WindowChromeFix.Apply(this);

        _viewModel = viewModel;
        _trayIconService = trayIconService;
        DataContext = viewModel;

        Closing += OnClosing;
        Closed += OnClosed;
        Drop += OnDrop;
        StateChanged += (_, _) => UpdateMaximizeRestoreGlyph();
        UpdateMaximizeRestoreGlyph();
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

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void ProfileTab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _profileDragStart = e.GetPosition(null);
        _profileDragCandidate = sender is Button { DataContext: ProfileTabViewModel { IsDefault: false } };
    }

    private void ProfileTab_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_profileDragCandidate || e.LeftButton != MouseButtonState.Pressed || sender is not Button { DataContext: ProfileTabViewModel tab } button)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _profileDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _profileDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _profileDragCandidate = false;
        DragDrop.DoDragDrop(button, tab.Name, DragDropEffects.Move);
    }

    private void ProfileTab_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Button { DataContext: ProfileTabViewModel target } ||
            !e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.StringFormat) is string draggedName)
        {
            _viewModel.ReorderProfile(draggedName, target.Name);
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeRestoreGlyph() =>
        MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "" : "";

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
