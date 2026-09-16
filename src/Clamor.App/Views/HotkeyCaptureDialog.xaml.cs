using System.Windows;
using System.Windows.Input;
using Clamor.App.Services;
using Clamor.Core.Models;

namespace Clamor.App.Views;

public partial class HotkeyCaptureDialog : Window
{
    private static readonly Key[] ModifierKeyList =
    {
        Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
    };

    public HotkeyBinding? Result { get; private set; }

    public HotkeyCaptureDialog()
    {
        InitializeComponent();
        WindowChromeFix.Apply(this);
        Loaded += (_, _) => Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (ModifierKeyList.Contains(key))
        {
            return;
        }

        e.Handled = true;

        if (key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyModifiers.Windows;

        Result = new HotkeyBinding
        {
            Modifiers = modifiers,
            VirtualKeyCode = KeyInterop.VirtualKeyFromKey(key),
        };
        DialogResult = true;
    }

    public static HotkeyBinding? Capture(Window? owner)
    {
        var dialog = new HotkeyCaptureDialog { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
