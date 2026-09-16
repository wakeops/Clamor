using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clamor.App.Views.Controls;

/// <summary>
/// Custom draggable title bar used by windows since <see cref="Window.WindowStyle"/> is None
/// (see the default Window style in Themes/Controls.xaml).
/// </summary>
public partial class TitleBar : UserControl
{
    public static readonly DependencyProperty ShowMinimizeProperty =
        DependencyProperty.Register(nameof(ShowMinimize), typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowMaximizeProperty =
        DependencyProperty.Register(nameof(ShowMaximize), typeof(bool), typeof(TitleBar), new PropertyMetadata(false));

    public TitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public bool ShowMinimize
    {
        get => (bool)GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    public bool ShowMaximize
    {
        get => (bool)GetValue(ShowMaximizeProperty);
        set => SetValue(ShowMaximizeProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        window.StateChanged += (_, _) => UpdateMaximizeGlyph(window);
        UpdateMaximizeGlyph(window);
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        if (e.ClickCount == 2 && ShowMaximize)
        {
            ToggleMaximize(window);
            return;
        }

        window.DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            ToggleMaximize(window);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeGlyph(Window window) =>
        MaximizeRestoreButton.Content = window.WindowState == WindowState.Maximized ? "" : "";
}
