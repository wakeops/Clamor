namespace Clamor.Core.Models;

/// <summary>App-level configuration, persisted to settings.json — one instance, not per-profile.</summary>
public sealed class AppSettings
{
    /// <summary>NAudio device id of the virtual-cable output (what Discord/Zoom hears).</summary>
    public string? OutputDeviceId { get; set; }

    /// <summary>NAudio device id of the monitor output (what the user hears).</summary>
    public string? MonitorDeviceId { get; set; }

    /// <summary>NAudio device id of the real microphone used for passthrough.</summary>
    public string? InputDeviceId { get; set; }

    public bool MicPassthroughEnabled { get; set; }

    public bool MinimizeToTrayOnClose { get; set; } = true;

    public string ActiveProfileName { get; set; } = "Default";

    public HotkeyBinding? StopAllHotkey { get; set; }

    /// <summary>Set once the user has dismissed the first-run "install VB-Cable?" prompt.</summary>
    public bool VbCablePromptDismissed { get; set; }
}
