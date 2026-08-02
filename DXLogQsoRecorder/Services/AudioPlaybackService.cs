using NAudio.Wave;

namespace DXLogQsoRecorder.Services;

public sealed class AudioPlaybackService : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    public event Action? PlaybackStopped;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _output?.PlaybackState == PlaybackState.Paused;
    public TimeSpan CurrentTime => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

    public void Play(string path)
    {
        Stop();
        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        _output.Init(_reader);
        _output.PlaybackStopped += OnStopped;
        _output.Play();
    }
    public void TogglePause()
    {
        if (_output is null) return;
        if (_output.PlaybackState == PlaybackState.Playing) _output.Pause();
        else if (_output.PlaybackState == PlaybackState.Paused) _output.Play();
    }
    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > _reader.TotalTime) position = _reader.TotalTime;
        _reader.CurrentTime = position;
    }
    public void Stop()
    {
        if (_output is not null) { _output.PlaybackStopped -= OnStopped; _output.Stop(); _output.Dispose(); _output = null; }
        _reader?.Dispose(); _reader = null;
    }
    private void OnStopped(object? sender, StoppedEventArgs e) => PlaybackStopped?.Invoke();
    public void Dispose() => Stop();
}
