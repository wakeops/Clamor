using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clamor.App.Services;

/// <summary>
/// Asks DWM to round the window's actual corners (Windows 11+) rather than faking it by
/// clipping our own content — that keeps the real window shape, shadow, and resize/snap
/// behavior all consistent with WindowChrome, which a purely visual (AllowsTransparency-based)
/// rounded-corner trick can't do cleanly. No-ops harmlessly on older Windows.
/// </summary>
public static class WindowCornerFix
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is not HwndSource hwndSource)
            {
                return;
            }

            var preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwndSource.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        };
    }
}
