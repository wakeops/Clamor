using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Clamor.Audio;

/// <summary>
/// Captures the real microphone and exposes it as a sample provider matching the engine's
/// mixing format, so <see cref="AudioEngine"/> can feed it into the cable mixer alongside
/// whatever clips are playing.
/// </summary>
public sealed class MicPassthrough : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private WasapiCapture? _capture;
    private MMDevice? _device;

    public bool IsActive => _capture is not null;

    public MicPassthrough(MMDeviceEnumerator enumerator)
    {
        _enumerator = enumerator;
    }

    public ISampleProvider Start(string inputDeviceId, WaveFormat targetFormat)
    {
        Stop();

        _device = _enumerator.GetDevice(inputDeviceId);
        _capture = new WasapiCapture(_device);

        var buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(500),
        };

        _capture.DataAvailable += (_, e) => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

        ISampleProvider provider = buffer.ToSampleProvider();
        provider = AudioFormatUtils.MatchChannels(provider, targetFormat.Channels);
        provider = AudioFormatUtils.MatchSampleRate(provider, targetFormat.SampleRate);

        _capture.StartRecording();
        return provider;
    }

    public void Stop()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;

        _device?.Dispose();
        _device = null;
    }

    public void Dispose() => Stop();
}
