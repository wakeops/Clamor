using Clamor.Core.Models;
using Xunit;

namespace Clamor.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void ToDisplayString_CombinesModifiersAndKeyName()
    {
        var binding = new HotkeyBinding
        {
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKeyCode = 0x70, // VK_F1
        };

        Assert.Equal("Ctrl+Shift+F1", binding.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_HasNoModifiers_WhenNoneSet()
    {
        var binding = new HotkeyBinding { Modifiers = HotkeyModifiers.None, VirtualKeyCode = 0x41 }; // 'A'

        Assert.Equal("A", binding.ToDisplayString());
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x30, "0")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x20, "Space")]
    [InlineData(0x0D, "Enter")]
    public void ToDisplayName_MapsKnownVirtualKeyCodes(int vk, string expected)
    {
        Assert.Equal(expected, VirtualKeyNames.ToDisplayName(vk));
    }

    [Fact]
    public void ToDisplayName_FallsBackToHex_ForUnknownCode()
    {
        Assert.Equal("VK_F0", VirtualKeyNames.ToDisplayName(0xF0));
    }
}
