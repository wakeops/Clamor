using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Clamor.Audio;

/// <summary>
/// Enumerates playback/capture endpoints for the settings UI. Deliberately separate from
/// <see cref="AudioEngine"/>'s own device handling — this is read-only discovery, the engine
/// owns opening and the lifetime of the devices it actually plays through.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    /// <summary>VB-Audio Virtual Cable's default playback endpoint name.</summary>
    public const string VbCableDeviceNameFragment = "CABLE Input";

    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices() => GetDevices(DataFlow.Render);

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices() => GetDevices(DataFlow.Capture);

    /// <summary>True if a VB-Audio Virtual Cable playback endpoint is currently installed and active.</summary>
    public bool IsVbCableInstalled() =>
        GetOutputDevices().Any(d => d.Name.Contains(VbCableDeviceNameFragment, StringComparison.OrdinalIgnoreCase));

    private List<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        string? defaultId = null;
        try
        {
            using var defaultDevice = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            defaultId = defaultDevice.ID;
        }
        catch (COMException)
        {
            // No default endpoint configured for this flow (e.g. no microphone attached) —
            // fine, it just means nothing gets marked as the default in the returned list.
        }

        var devices = new List<AudioDeviceInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
            device.Dispose();
        }

        return devices;
    }

    public void Dispose() => _enumerator.Dispose();
}
