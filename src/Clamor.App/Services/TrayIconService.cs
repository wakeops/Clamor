using System.Drawing;
using System.Windows.Forms;

namespace Clamor.App.Services;

/// <summary>
/// Wraps a WinForms <see cref="NotifyIcon"/> — WPF has no first-party tray icon control.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? ShowRequested;
    public event EventHandler? StopAllRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Clamor", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Stop All", null, (_, _) => StopAllRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Clamor",
            ContextMenuStrip = menu,
            Visible = false,
        };

        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Hide() => _notifyIcon.Visible = false;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    /// <summary>Reuses the .exe's own icon (set via ApplicationIcon in the csproj) so the tray,
    /// taskbar, and shortcut all show the same artwork from one source.</summary>
    private static Icon LoadAppIcon()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return SystemIcons.Application;
    }
}
