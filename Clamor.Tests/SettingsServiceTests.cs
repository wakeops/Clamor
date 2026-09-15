using Clamor.Core.Models;
using Clamor.Core.Services;
using Clamor.Tests.TestSupport;
using Xunit;

namespace Clamor.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsMissing()
    {
        using var dir = new TempDirectory();
        var service = new SettingsService(dir.Path);

        var settings = service.Load();

        Assert.Equal("Default", settings.ActiveProfileName);
        Assert.True(settings.MinimizeToTrayOnClose);
        Assert.False(settings.MicPassthroughEnabled);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        using var dir = new TempDirectory();
        var service = new SettingsService(dir.Path);

        var settings = new AppSettings
        {
            OutputDeviceId = "cable-1",
            MonitorDeviceId = "speakers-1",
            InputDeviceId = "mic-1",
            MicPassthroughEnabled = true,
            ActiveProfileName = "Streaming",
            StopAllHotkey = new HotkeyBinding { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift, VirtualKeyCode = 0x1B },
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal("cable-1", loaded.OutputDeviceId);
        Assert.Equal("speakers-1", loaded.MonitorDeviceId);
        Assert.Equal("mic-1", loaded.InputDeviceId);
        Assert.True(loaded.MicPassthroughEnabled);
        Assert.Equal("Streaming", loaded.ActiveProfileName);
        Assert.NotNull(loaded.StopAllHotkey);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, loaded.StopAllHotkey!.Modifiers);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{ not valid json ");
        var service = new SettingsService(dir.Path);

        var settings = service.Load();

        Assert.Equal("Default", settings.ActiveProfileName);
    }
}
