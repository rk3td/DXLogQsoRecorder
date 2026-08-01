using DXLogQsoRecorder.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DXLogQsoRecorder.Services;

public sealed class AudioCaptureService : IDisposable
{
    private readonly object _sync = new();
    private readonly LinkedList<AudioChunk> _buffer = new();
    private WasapiCapture? _capture;
    private int _bufferBytes;
    private int _maxBufferBytes;

    public event Action<byte[], WaveFormat>? AudioData;
    public event Action<float>? LevelChanged;
    public WaveFormat? WaveFormat => _capture?.WaveFormat;
    public bool IsRunning => _capture is not null;

    public IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName })
            .ToList();
    }

    public void Start(string deviceId, int preBufferSeconds)
    {
        if (_capture is not null) throw new InvalidOperationException("Audio capture is already running.");
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        _capture = new WasapiCapture(device);
        LogService.Write($"[INFO] Audio capture started: device={device.FriendlyName}; format={_capture.WaveFormat.SampleRate} Hz, {_capture.WaveFormat.BitsPerSample}-bit, {_capture.WaveFormat.Channels} channel(s), {_capture.WaveFormat.Encoding}");
        _maxBufferBytes = Math.Max(1, _capture.WaveFormat.AverageBytesPerSecond * preBufferSeconds);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, e) => { if (e.Exception is not null) LogService.Write("Audio error: " + e.Exception); };
        _capture.StartRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var copy = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
        lock (_sync)
        {
            _buffer.AddLast(new AudioChunk(copy));
            _bufferBytes += copy.Length;
            while (_bufferBytes > _maxBufferBytes && _buffer.First is not null)
            {
                _bufferBytes -= _buffer.First.Value.Data.Length;
                _buffer.RemoveFirst();
            }
        }
        var capture = _capture;
        if (capture is null) return;

        AudioData?.Invoke(copy, capture.WaveFormat);
        LevelChanged?.Invoke(CalculatePeak(copy, capture.WaveFormat));
    }

    public byte[] SnapshotPreBuffer()
    {
        lock (_sync)
        {
            var result = new byte[_bufferBytes];
            var offset = 0;
            foreach (var chunk in _buffer)
            {
                Buffer.BlockCopy(chunk.Data, 0, result, offset, chunk.Data.Length);
                offset += chunk.Data.Length;
            }
            return result;
        }
    }

    public void Stop()
    {
        if (_capture is null) return;
        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
        lock (_sync) { _buffer.Clear(); _bufferBytes = 0; }
    }

    private static float CalculatePeak(byte[] buffer, WaveFormat format)
    {
        try
        {
            float peak = 0;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                for (var i = 0; i + 3 < buffer.Length; i += 4)
                    peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(buffer, i)));
            }
            else if (format.BitsPerSample == 16)
            {
                for (var i = 0; i + 1 < buffer.Length; i += 2)
                    peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f));
            }
            return Math.Clamp(peak, 0, 1);
        }
        catch { return 0; }
    }

    public void Dispose() => Stop();
    private sealed record AudioChunk(byte[] Data);
}
