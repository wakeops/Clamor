namespace Clamor.Core.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// A global hotkey, stored as a Win32 virtual-key code so Clamor.Hotkeys can hand it to
/// RegisterHotKey without re-translating from a UI framework's key enum.
/// </summary>
public sealed class HotkeyBinding
{
    public HotkeyModifiers Modifiers { get; set; }

    public int VirtualKeyCode { get; set; }

    public string ToDisplayString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(VirtualKeyNames.ToDisplayName(VirtualKeyCode));
        return string.Join("+", parts);
    }

    public override string ToString() => ToDisplayString();
}

/// <summary>
/// Human-readable names for the subset of Win32 virtual-key codes a soundboard hotkey is
/// realistically bound to. Lives in Core (no WPF reference) so both the UI and tests can
/// render a binding without pulling in PresentationCore's Key enum.
/// </summary>
public static class VirtualKeyNames
{
    private static readonly Dictionary<int, string> Names = BuildNames();

    public static string ToDisplayName(int virtualKeyCode)
    {
        if (Names.TryGetValue(virtualKeyCode, out var name))
        {
            return name;
        }

        // Printable ASCII range (letters/digits share their VK code with their char code).
        if (virtualKeyCode is >= 0x30 and <= 0x5A)
        {
            return ((char)virtualKeyCode).ToString();
        }

        return $"VK_{virtualKeyCode:X2}";
    }

    private static Dictionary<int, string> BuildNames()
    {
        var names = new Dictionary<int, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x1B] = "Esc",
            [0x20] = "Space",
            [0x21] = "Page Up",
            [0x22] = "Page Down",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
        };

        for (var f = 1; f <= 24; f++)
        {
            names[0x6F + f] = $"F{f}"; // VK_F1 = 0x70
        }

        for (var n = 0; n <= 9; n++)
        {
            names[0x60 + n] = $"Numpad {n}";
        }

        return names;
    }
}
