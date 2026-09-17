using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Clamor.App.Services;

/// <summary>
/// WindowChrome's CaptionHeight="0" (set in the default Window style) is normally enough to
/// suppress the native title bar, but some Windows 11 builds still paint a thin system caption
/// strip on top of it regardless. Intercepting WM_NCCALCSIZE and accepting the proposed client
/// rectangle as-is (instead of letting Windows reserve space for a caption) removes it for good.
///
/// That also removes WindowChrome's own resize-border hit-testing, though — with zero non-client
/// area, WindowChrome has nothing left to compute ResizeBorderThickness against, so every edge
/// reports HTCLIENT and dragging the window border does nothing. Since we've taken over the
/// non-client area entirely, we take over WM_NCHITTEST too and do the edge/corner math ourselves.
/// </summary>
public static class WindowChromeFix
{
    private const int WM_NCCALCSIZE = 0x0083;
    private const int WM_NCHITTEST = 0x0084;
    private const int ResizeBorderDips = 6;

    private const nint HTLEFT = 10;
    private const nint HTRIGHT = 11;
    private const nint HTTOP = 12;
    private const nint HTTOPLEFT = 13;
    private const nint HTTOPRIGHT = 14;
    private const nint HTBOTTOM = 15;
    private const nint HTBOTTOMLEFT = 16;
    private const nint HTBOTTOMRIGHT = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out Rect rect);

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                hwndSource.AddHook((hwnd, msg, wParam, lParam, ref handled) =>
                    WndProc(window, hwnd, msg, wParam, lParam, ref handled));
            }
        };
    }

    private static nint WndProc(Window window, nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_NCCALCSIZE && wParam != 0)
        {
            handled = true;
            return 0;
        }

        if (msg == WM_NCHITTEST)
        {
            var hitTestResult = HitTestResizeBorder(window, hwnd, lParam);
            if (hitTestResult is nint result)
            {
                handled = true;
                return result;
            }
        }

        return 0;
    }

    /// <summary>Null means "not near an edge" — the caller leaves <c>handled</c> false so Windows'
    /// own default processing (HTCLIENT for everything under CaptionHeight="0") still applies.</summary>
    private static nint? HitTestResizeBorder(Window window, nint hwnd, nint lParam)
    {
        if (window.ResizeMode is ResizeMode.NoResize or ResizeMode.CanMinimize ||
            window.WindowState == WindowState.Maximized ||
            !GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));

        var dpiScale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        var border = (int)Math.Round(ResizeBorderDips * dpiScale);

        var onLeft = x < rect.Left + border;
        var onRight = x >= rect.Right - border;
        var onTop = y < rect.Top + border;
        var onBottom = y >= rect.Bottom - border;

        return (onTop, onBottom, onLeft, onRight) switch
        {
            (true, _, true, _) => HTTOPLEFT,
            (true, _, _, true) => HTTOPRIGHT,
            (_, true, true, _) => HTBOTTOMLEFT,
            (_, true, _, true) => HTBOTTOMRIGHT,
            (true, _, _, _) => HTTOP,
            (_, true, _, _) => HTBOTTOM,
            (_, _, true, _) => HTLEFT,
            (_, _, _, true) => HTRIGHT,
            _ => null,
        };
    }
}
