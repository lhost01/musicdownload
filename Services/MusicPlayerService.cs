using Avalonia.Threading;
using NAudio.Wave;

namespace Musicbox.Services;

public sealed class MusicPlayerService : IDisposable
{
    private readonly DispatcherTimer _positionTimer;
    private WaveOutEvent? _outputDevice;
    private AudioFileReader? _audioFile;
    private bool _stopRequested;
    private float _volume = 0.75f;

    public bool IsPlaying { get; private set; }

    public bool IsPaused { get; private set; }

    public TimeSpan CurrentPosition => _audioFile?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan TotalDuration => _audioFile?.TotalTime ?? TimeSpan.Zero;

    public event EventHandler? PlaybackStarted;

    public event EventHandler? PlaybackPaused;

    public event EventHandler? PlaybackStopped;

    public event EventHandler? PlaybackCompleted;

    public event EventHandler<TimeSpan>? PositionChanged;

    public event EventHandler<TimeSpan>? DurationChanged;

    public MusicPlayerService()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _positionTimer.Tick += (_, _) => PositionChanged?.Invoke(this, CurrentPosition);
    }

    public void Play(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        StopInternal(false);

        _audioFile = new AudioFileReader(filePath);
        _outputDevice = new WaveOutEvent();
        _outputDevice.PlaybackStopped += OutputDeviceOnPlaybackStopped;
        _outputDevice.Init(_audioFile);
        _audioFile.Volume = _volume;
        _outputDevice.Play();

        IsPlaying = true;
        IsPaused = false;
        _stopRequested = false;
        _positionTimer.Start();
        DurationChanged?.Invoke(this, TotalDuration);
        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_outputDevice is null || !IsPlaying || IsPaused)
        {
            return;
        }

        _outputDevice.Pause();
        IsPaused = true;
        _positionTimer.Stop();
        PlaybackPaused?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        if (_outputDevice is null || !IsPlaying || !IsPaused)
        {
            return;
        }

        _outputDevice.Play();
        IsPaused = false;
        _positionTimer.Start();
        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        StopInternal(true);
    }

    public void SetPosition(TimeSpan position)
    {
        if (_audioFile is null)
        {
            return;
        }

        var clamped = position < TimeSpan.Zero
            ? TimeSpan.Zero
            : position > _audioFile.TotalTime
                ? _audioFile.TotalTime
                : position;

        _audioFile.CurrentTime = clamped;
        PositionChanged?.Invoke(this, clamped);
    }

    public void SetVolume(double value)
    {
        if (_audioFile is not null)
        {
            _volume = (float)Math.Clamp(value, 0, 1);
            _audioFile.Volume = _volume;
        }
        else
        {
            _volume = (float)Math.Clamp(value, 0, 1);
        }
    }

    private void OutputDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _positionTimer.Stop();
        var completed = !_stopRequested && _audioFile is not null && _audioFile.Position >= _audioFile.Length;
        IsPlaying = false;
        IsPaused = false;

        if (completed)
        {
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StopInternal(bool raiseStopped)
    {
        _stopRequested = true;
        _positionTimer.Stop();

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDeviceOnPlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        _audioFile?.Dispose();
        _audioFile = null;

        var wasPlaying = IsPlaying || IsPaused;
        IsPlaying = false;
        IsPaused = false;

        if (raiseStopped && wasPlaying)
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        StopInternal(false);
    }
}
