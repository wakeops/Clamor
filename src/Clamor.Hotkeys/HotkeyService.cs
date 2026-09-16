using System.Collections.Concurrent;
using Clamor.Core.Models;

namespace Clamor.Hotkeys;

/// <summary>
/// Global hotkeys that fire whether or not Clamor is focused, via Win32 RegisterHotKey. Owns a
/// dedicated background thread running a message-only window and its own GetMessage loop, since
/// RegisterHotKey/UnregisterHotKey are thread-affine to whichever thread created that window —
/// callers can invoke <see cref="Register"/>/<see cref="Unregister"/> from any thread (typically
/// the UI thread); the call is marshaled onto the message thread via SendMessage and blocks
/// until it completes.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const uint WM_INVOKE = NativeMethods.WM_APP + 1;

    private readonly NativeMethods.WndProcDelegate _wndProcDelegate;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _windowReady = new(false);
    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private readonly object _registrationsLock = new();
    private readonly Dictionary<int, HotkeyBinding> _registrations = new();

    private IntPtr _hwnd;
    private int _nextId;
    private bool _disposed;

    /// <summary>Fires on the hotkey message thread with the id returned by <see cref="Register"/>.
    /// Subscribers must marshal back to the UI thread before touching UI state.</summary>
    public event EventHandler<int>? HotkeyPressed;

    public HotkeyService()
    {
        _wndProcDelegate = WndProc;
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Clamor.Hotkeys" };
        _thread.Start();

        if (!_windowReady.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("Timed out waiting for the hotkey message window to initialize.");
        }

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the hotkey message window.");
        }
    }

    /// <summary>Registers a global hotkey and returns an id to pass to <see cref="Unregister"/>.
    /// Throws if the combination is already claimed by another application.</summary>
    public int Register(HotkeyBinding binding)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = 0;
        Exception? failure = null;
        using var done = new ManualResetEventSlim(false);

        RunOnMessageThread(() =>
        {
            id = Interlocked.Increment(ref _nextId);
            var modifiers = ToNativeModifiers(binding.Modifiers) | NativeMethods.MOD_NOREPEAT;

            if (!NativeMethods.RegisterHotKey(_hwnd, id, modifiers, (uint)binding.VirtualKeyCode))
            {
                failure = new InvalidOperationException(
                    $"Could not register hotkey '{binding.ToDisplayString()}' — it may already be bound elsewhere.");
            }
            else
            {
                lock (_registrationsLock)
                {
                    _registrations[id] = binding;
                }
            }

            done.Set();
        });

        done.Wait();

        if (failure is not null)
        {
            throw failure;
        }

        return id;
    }

    public void Unregister(int id)
    {
        if (_disposed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        RunOnMessageThread(() =>
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
            lock (_registrationsLock)
            {
                _registrations.Remove(id);
            }
        });
    }

    public void UnregisterAll()
    {
        List<int> ids;
        lock (_registrationsLock)
        {
            ids = _registrations.Keys.ToList();
        }

        foreach (var id in ids)
        {
            Unregister(id);
        }
    }

    private void RunOnMessageThread(Action action)
    {
        _pendingActions.Enqueue(action);
        NativeMethods.SendMessageW(_hwnd, WM_INVOKE, IntPtr.Zero, IntPtr.Zero);
    }

    private void ThreadMain()
    {
        var className = $"ClamorHotkeyWindow_{Guid.NewGuid():N}";
        var hInstance = NativeMethods.GetModuleHandleW(null);
        var wndClass = new NativeMethods.WNDCLASS
        {
            lpfnWndProc = _wndProcDelegate,
            lpszClassName = className,
            hInstance = hInstance,
        };

        if (NativeMethods.RegisterClassW(ref wndClass) == 0)
        {
            _windowReady.Set();
            return;
        }

        _hwnd = NativeMethods.CreateWindowExW(
            0, className, "ClamorHotkeyWindow", 0,
            0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);

        _windowReady.Set();

        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        while (NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        NativeMethods.UnregisterClassW(className, hInstance);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                HotkeyPressed?.Invoke(this, unchecked((int)(long)wParam));
                return IntPtr.Zero;

            case WM_INVOKE:
                while (_pendingActions.TryDequeue(out var action))
                {
                    action();
                }

                return IntPtr.Zero;

            default:
                return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) result |= NativeMethods.MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();

        if (_hwnd != IntPtr.Zero)
        {
            var hwnd = _hwnd;
            RunOnMessageThread(() =>
            {
                NativeMethods.DestroyWindow(hwnd);
                NativeMethods.PostQuitMessage(0);
            });
        }

        _thread.Join(TimeSpan.FromSeconds(2));
        _windowReady.Dispose();
    }
}
