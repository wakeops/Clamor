using System.Windows;
using System.Windows.Interop;

namespace Clamor.App.Services;

/// <summary>
/// WindowChrome's CaptionHeight="0" (set in the default Window style) is normally enough to
/// suppress the native title bar, but some Windows 11 builds still paint a thin system caption
/// strip on top of it regardless. Intercepting WM_NCCALCSIZE and accepting the proposed client
/// rectangle as-is (instead of letting Windows reserve space for a caption) removes it for good.
/// </summary>
public static class WindowChromeFix
{
    private const int WM_NCCALCSIZE = 0x0083;

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        };
    }

    private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_NCCALCSIZE && wParam != 0)
        {
            handled = true;
        }

        return 0;
    }
}
