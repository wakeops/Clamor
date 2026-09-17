using Clamor.Core.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Clamor.Audio;

/// <summary>
/// Owns the two live mixing buses (virtual-cable output and monitor output) and the devices
/// they play through. Every clip press adds a pair of <see cref="ClipPlayer"/> instances — one
/// per bus — so what Discord/Zoom hears and what the user hears both get every clip, while mic
/// passthrough only ever feeds the cable bus.
///
/// All mixer/device work happens on NAudio's own callback threads. <see cref="ClipStarted"/>
/// and <see cref="ClipStopped"/> can fire off the UI thread — subscribers must marshal back
/// (e.g. <c>Dispatcher.Invoke</c>) before touching UI state.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int LatencyMs = 50;

    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private readonly MixingSampleProvider _cableMixer;
    private readonly MixingSampleProvider _monitorMixer;
    private readonly MicPassthrough _micPassthrough;

    private readonly object _voiceLock = new();
    private readonly Dictionary<Guid, Voice> _voices = new();
    private readonly Dictionary<ISampleProvider, Guid> _providerToVoice = new();

    private WasapiOut? _cableOut;
    private WasapiOut? _monitorOut;
    private MMDevice? _cableDevice;
    private MMDevice? _monitorDevice;
    private ISampleProvider? _micMixerInput;
    private VolumeSampleProvider? _cableVolume;
    private VolumeSampleProvider? _monitorVolume;
    private float _masterVolume = 1f;

    public event EventHandler<Guid>? ClipStarted;
    public event EventHandler<Guid>? ClipStopped;

    public bool IsMicPassthroughActive => _micPassthrough.IsActive;

    /// <summary>Linear gain (0.0-1.0) applied to both output buses, on top of each clip's own
    /// volume. Settable at any time, including before outputs have started.</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            if (_cableVolume is not null)
            {
                _cableVolume.Volume = _masterVolume;
            }

            if (_monitorVolume is not null)
            {
                _monitorVolume.Volume = _masterVolume;
            }
        }
    }

    public AudioEngine()
    {
        _cableMixer = new MixingSampleProvider(_format) { ReadFully = true };
        _monitorMixer = new MixingSampleProvider(_format) { ReadFully = true };
        _micPassthrough = new MicPassthrough(_deviceEnumerator);

        _cableMixer.MixerInputEnded += (_, e) => OnMixerInputEnded(e.SampleProvider, isCable: true);
        _monitorMixer.MixerInputEnded += (_, e) => OnMixerInputEnded(e.SampleProvider, isCable: false);
    }

    public void StartOutputs(string cableDeviceId, string monitorDeviceId)
    {
        StopOutputs();

        _cableDevice = _deviceEnumerator.GetDevice(cableDeviceId);
        _cableOut = new WasapiOut(_cableDevice, AudioClientShareMode.Shared, true, LatencyMs);
        _cableVolume = new VolumeSampleProvider(_cableMixer) { Volume = _masterVolume };
        _cableOut.Init(_cableVolume);
        _cableOut.Play();

        _monitorDevice = _deviceEnumerator.GetDevice(monitorDeviceId);
        _monitorOut = new WasapiOut(_monitorDevice, AudioClientShareMode.Shared, true, LatencyMs);
        _monitorVolume = new VolumeSampleProvider(_monitorMixer) { Volume = _masterVolume };
        _monitorOut.Init(_monitorVolume);
        _monitorOut.Play();
    }

    public void StopOutputs()
    {
        _cableOut?.Stop();
        _cableOut?.Dispose();
        _cableOut = null;
        _cableDevice?.Dispose();
        _cableDevice = null;

        _monitorOut?.Stop();
        _monitorOut?.Dispose();
        _monitorOut = null;
        _monitorDevice?.Dispose();
        _monitorDevice = null;

        _cableVolume = null;
        _monitorVolume = null;
    }

    public void StartMicPassthrough(string inputDeviceId)
    {
        StopMicPassthrough();
        _micMixerInput = _micPassthrough.Start(inputDeviceId, _format);
        _cableMixer.AddMixerInput(_micMixerInput);
    }

    public void StopMicPassthrough()
    {
        if (_micMixerInput is not null)
        {
            _cableMixer.RemoveMixerInput(_micMixerInput);
            _micMixerInput = null;
        }

        _micPassthrough.Stop();
    }

    public Guid PlayClip(SoundClip clip) => PlayClip(clip.FilePath, clip.Volume);

    public Guid PlayClip(string filePath, double volume)
    {
        var voiceId = Guid.NewGuid();
        var cablePlayer = new ClipPlayer(filePath, (float)volume, _format);
        var monitorPlayer = new ClipPlayer(filePath, (float)volume, _format);
        var voice = new Voice(cablePlayer, monitorPlayer);

        lock (_voiceLock)
        {
            _voices[voiceId] = voice;
            _providerToVoice[cablePlayer.Output] = voiceId;
            _providerToVoice[monitorPlayer.Output] = voiceId;
        }

        _cableMixer.AddMixerInput(cablePlayer.Output);
        _monitorMixer.AddMixerInput(monitorPlayer.Output);

        ClipStarted?.Invoke(this, voiceId);
        return voiceId;
    }

    public void StopClip(Guid voiceId)
    {
        Voice? voice;
        lock (_voiceLock)
        {
            if (!_voices.Remove(voiceId, out voice))
            {
                return;
            }

            _providerToVoice.Remove(voice.CablePlayer.Output);
            _providerToVoice.Remove(voice.MonitorPlayer.Output);
        }

        _cableMixer.RemoveMixerInput(voice.CablePlayer.Output);
        _monitorMixer.RemoveMixerInput(voice.MonitorPlayer.Output);
        voice.Dispose();
        ClipStopped?.Invoke(this, voiceId);
    }

    public void StopAll()
    {
        List<Guid> ids;
        lock (_voiceLock)
        {
            ids = _voices.Keys.ToList();
        }

        foreach (var id in ids)
        {
            StopClip(id);
        }
    }

    /// <summary>
    /// A voice is two players (cable + monitor) that reached end-of-file independently — each
    /// mixer detects and removes its own finished input on its own callback thread. The voice
    /// is only torn down once <em>both</em> sides have reported ending, so a few milliseconds
    /// of clock drift between the two output devices can never leave a dangling half-voice.
    /// </summary>
    private void OnMixerInputEnded(ISampleProvider provider, bool isCable)
    {
        Voice? finishedVoice = null;
        Guid voiceId = default;

        lock (_voiceLock)
        {
            if (!_providerToVoice.TryGetValue(provider, out voiceId))
            {
                return;
            }

            if (!_voices.TryGetValue(voiceId, out var voice))
            {
                return;
            }

            if (isCable)
            {
                voice.CableEnded = true;
            }
            else
            {
                voice.MonitorEnded = true;
            }

            if (!voice.CableEnded || !voice.MonitorEnded)
            {
                return;
            }

            _voices.Remove(voiceId);
            _providerToVoice.Remove(voice.CablePlayer.Output);
            _providerToVoice.Remove(voice.MonitorPlayer.Output);
            finishedVoice = voice;
        }

        finishedVoice.Dispose();
        ClipStopped?.Invoke(this, voiceId);
    }

    public void Dispose()
    {
        StopAll();
        StopMicPassthrough();
        StopOutputs();
        _deviceEnumerator.Dispose();
    }

    private sealed class Voice : IDisposable
    {
        public ClipPlayer CablePlayer { get; }
        public ClipPlayer MonitorPlayer { get; }
        public bool CableEnded { get; set; }
        public bool MonitorEnded { get; set; }

        public Voice(ClipPlayer cablePlayer, ClipPlayer monitorPlayer)
        {
            CablePlayer = cablePlayer;
            MonitorPlayer = monitorPlayer;
        }

        public void Dispose()
        {
            CablePlayer.Dispose();
            MonitorPlayer.Dispose();
        }
    }
}
