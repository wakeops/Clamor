using Clamor.Core.Models;

namespace Clamor.App.ViewModels;

/// <summary>
/// Backs the Playback drawer for one clip. Edits a working copy of the clip's
/// <see cref="ClipPlaybackSettings"/> and hands every change to the save callback, so the drawer
/// has no Save button — like the grid's volume slider, changes apply as they're made. Volume is
/// edited straight through <see cref="Clip"/> so the drawer and grid sliders stay in step.
/// </summary>
public sealed class ClipPlaybackViewModel : ViewModelBase
{
    /// <summary>Shortest slice the trim handles can be pulled down to.</summary>
    private const double MinTrimSeconds = 0.05;

    private readonly ClipPlaybackSettings _settings;
    private readonly Action<ClipPlaybackSettings> _save;
    private bool _isPreviewing;

    public ClipPlaybackViewModel(
        SoundClipViewModel clip,
        TimeSpan? duration,
        Action<ClipPlaybackSettings> save,
        Action togglePreview,
        Action close)
    {
        Clip = clip;
        _settings = clip.Playback.Normalized();
        _save = save;
        DurationSeconds = duration?.TotalSeconds ?? 0;

        // A trim saved against a file that has since been swapped for a shorter one shouldn't
        // leave the handles off the end of the track.
        if (HasDuration && _settings.StartSeconds >= DurationSeconds)
        {
            _settings.StartSeconds = 0;
        }

        if (HasDuration && _settings.EndSeconds >= DurationSeconds)
        {
            _settings.EndSeconds = null;
        }

        TogglePreviewCommand = new RelayCommand(_ => togglePreview());
        CloseCommand = new RelayCommand(_ => close());
        ResetTrimCommand = new RelayCommand(_ => ResetTrim());
        ResetSpeedCommand = new RelayCommand(_ => Speed = 1.0);
    }

    public SoundClipViewModel Clip { get; }

    public RelayCommand TogglePreviewCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand ResetTrimCommand { get; }
    public RelayCommand ResetSpeedCommand { get; }

    /// <summary>0 when the file couldn't be read, in which case trimming is disabled.</summary>
    public double DurationSeconds { get; }

    public bool HasDuration => DurationSeconds > 0;

    public double StartSeconds
    {
        get => _settings.StartSeconds;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, EndSeconds - MinTrimSeconds));
            if (Math.Abs(value - _settings.StartSeconds) < 0.0005)
            {
                return;
            }

            _settings.StartSeconds = value;
            OnTrimChanged();
        }
    }

    public double EndSeconds
    {
        get => _settings.EndSeconds ?? DurationSeconds;
        set
        {
            value = Math.Clamp(value, Math.Min(DurationSeconds, StartSeconds + MinTrimSeconds), DurationSeconds);
            if (Math.Abs(value - EndSeconds) < 0.0005)
            {
                return;
            }

            // Pinned to the end of the file is stored as "no end", so the clip keeps playing to
            // the end even if the file is later re-recorded longer.
            _settings.EndSeconds = value >= DurationSeconds - 0.0005 ? null : value;
            OnTrimChanged();
        }
    }

    public string StartLabel => FormatTime(StartSeconds);
    public string EndLabel => FormatTime(EndSeconds);
    public string TrimmedLengthLabel => FormatTime((EndSeconds - StartSeconds) / Speed);

    public double Speed
    {
        get => _settings.Speed;
        set
        {
            // Snap to hundredths so the label and stored value agree.
            value = Math.Round(Math.Clamp(value, ClipPlaybackSettings.MinSpeed, ClipPlaybackSettings.MaxSpeed), 2);
            if (value == _settings.Speed)
            {
                return;
            }

            _settings.Speed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpeedLabel));
            OnPropertyChanged(nameof(TrimmedLengthLabel));
            Commit();
        }
    }

    public string SpeedLabel => $"{Speed:0.00}x";

    public bool RandomPlayEnabled
    {
        get => _settings.RandomPlayEnabled;
        set
        {
            if (value == _settings.RandomPlayEnabled)
            {
                return;
            }

            _settings.RandomPlayEnabled = value;
            OnPropertyChanged();
            Commit();
        }
    }

    /// <summary>Raising the minimum past the maximum drags the maximum up with it.</summary>
    public double RandomMinDelaySeconds
    {
        get => _settings.RandomMinDelaySeconds;
        set
        {
            value = Math.Max(ClipPlaybackSettings.MinRandomDelaySeconds, value);
            _settings.RandomMinDelaySeconds = value;
            if (_settings.RandomMaxDelaySeconds < value)
            {
                _settings.RandomMaxDelaySeconds = value;
            }

            OnRandomDelayChanged();
        }
    }

    /// <summary>Lowering the maximum below the minimum drags the minimum down with it.</summary>
    public double RandomMaxDelaySeconds
    {
        get => _settings.RandomMaxDelaySeconds;
        set
        {
            value = Math.Max(ClipPlaybackSettings.MinRandomDelaySeconds, value);
            _settings.RandomMaxDelaySeconds = value;
            if (_settings.RandomMinDelaySeconds > value)
            {
                _settings.RandomMinDelaySeconds = value;
            }

            OnRandomDelayChanged();
        }
    }

    public bool IsPreviewing
    {
        get => _isPreviewing;
        set
        {
            if (SetField(ref _isPreviewing, value))
            {
                OnPropertyChanged(nameof(PreviewButtonLabel));
            }
        }
    }

    public string PreviewButtonLabel => IsPreviewing ? "Stop Preview" : "Preview";

    private void ResetTrim()
    {
        _settings.StartSeconds = 0;
        _settings.EndSeconds = null;
        OnTrimChanged();
    }

    private void OnTrimChanged()
    {
        OnPropertyChanged(nameof(StartSeconds));
        OnPropertyChanged(nameof(EndSeconds));
        OnPropertyChanged(nameof(StartLabel));
        OnPropertyChanged(nameof(EndLabel));
        OnPropertyChanged(nameof(TrimmedLengthLabel));
        Commit();
    }

    private void OnRandomDelayChanged()
    {
        // Always raised, even when the value didn't change, so a TextBox showing a rejected
        // entry (e.g. "0") snaps back to the clamped value.
        OnPropertyChanged(nameof(RandomMinDelaySeconds));
        OnPropertyChanged(nameof(RandomMaxDelaySeconds));
        Commit();
    }

    private void Commit() => _save(_settings.Clone());

    private static string FormatTime(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.ff" : @"m\:ss\.ff");
}
