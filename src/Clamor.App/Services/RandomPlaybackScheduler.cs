using System.Windows.Threading;
using Clamor.Core.Models;

namespace Clamor.App.Services;

/// <summary>
/// Fires clips on their own after a random delay, for clips with
/// <see cref="ClipPlaybackSettings.RandomPlayEnabled"/> set. Each clip gets its own
/// <see cref="DispatcherTimer"/>, so plays land on the UI thread like a button press would, and
/// a new delay is drawn from the clip's window after every play.
/// </summary>
public sealed class RandomPlaybackScheduler : IDisposable
{
    private readonly Action<Guid> _play;
    private readonly Random _random = new();
    private readonly Dictionary<Guid, DispatcherTimer> _timers = new();

    public RandomPlaybackScheduler(Action<Guid> play)
    {
        _play = play;
    }

    /// <summary>(Re)starts the clip's countdown from a fresh delay, or stops it if random play
    /// is off.</summary>
    public void Update(Guid clipId, ClipPlaybackSettings playback)
    {
        Stop(clipId);
        if (!playback.RandomPlayEnabled)
        {
            return;
        }

        var settings = playback.Clone();
        var timer = new DispatcherTimer { Interval = settings.NextRandomDelay(_random) };
        timer.Tick += (_, _) =>
        {
            timer.Interval = settings.NextRandomDelay(_random);
            _play(clipId);
        };
        _timers[clipId] = timer;
        timer.Start();
    }

    public void Stop(Guid clipId)
    {
        if (_timers.Remove(clipId, out var timer))
        {
            timer.Stop();
        }
    }

    public void StopAll()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
        }

        _timers.Clear();
    }

    public void Dispose() => StopAll();
}
