using System.IO;
using DXLogQsoRecorder.Models;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DXLogQsoRecorder.Services;

public sealed class RecordingCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly AudioCaptureService _audio;
    private readonly List<PendingRecording> _pending = new();
    private readonly Func<AppSettings> _settingsProvider;
    private bool _disposed;

    public event Action<DxLogQso, string>? RecordingStarted;
    public event Action<DxLogQso, string>? RecordingCompleted;
    public event Action<DxLogQso, string, Exception>? Mp3EncodingFailed;
    public event Action<DxLogQso, Exception>? RecordingFailed;

    public RecordingCoordinator(AudioCaptureService audio, Func<AppSettings> settingsProvider)
    {
        _audio = audio;
        _settingsProvider = settingsProvider;
        _audio.AudioData += OnAudioData;
    }

    public void Begin(DxLogQso qso)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RecordingCoordinator));
        var format = _audio.WaveFormat ?? throw new InvalidOperationException("Audio capture is not running.");
        var settings = _settingsProvider();
        var outputRoot = Path.IsPathRooted(settings.OutputDirectory)
            ? settings.OutputDirectory
            : Path.Combine(PortablePaths.BaseDirectory, settings.OutputDirectory);
        var contestDirectory = FileNameBuilder.BuildContestDirectory(outputRoot, qso.ContestName);
        Directory.CreateDirectory(contestDirectory);

        var finalMp3Path = FileNameBuilder.EnsureUnique(contestDirectory, FileNameBuilder.Build(qso, ".mp3"));
        var tempPath = Path.Combine(PortablePaths.TempDirectory, $"{Guid.NewGuid():N}.tmp.wav");
        var writer = new WaveFileWriter(tempPath, format);
        var pre = _audio.SnapshotPreBuffer();
        writer.Write(pre, 0, pre.Length);

        var targetBytes = Math.Max(0, format.AverageBytesPerSecond * settings.PostBufferSeconds);
        var pending = new PendingRecording(qso, finalMp3Path, contestDirectory, tempPath, writer,
            targetBytes, settings.Mp3BitrateKbps);
        lock (_sync) _pending.Add(pending);
        LogService.Write($"[INFO] Recording started: {qso.Call}; contest={qso.ContestName}; target={finalMp3Path}");
        RecordingStarted?.Invoke(qso, finalMp3Path);
        if (targetBytes == 0) QueueCompletion(pending);
    }

    private void OnAudioData(byte[] data, WaveFormat format)
    {
        List<PendingRecording> completed = new();
        lock (_sync)
        {
            foreach (var p in _pending.ToList())
            {
                try
                {
                    var remaining = p.TargetPostBytes - p.WrittenPostBytes;
                    var count = Math.Min(data.Length, Math.Max(0, remaining));
                    if (count > 0)
                    {
                        p.Writer.Write(data, 0, count);
                        p.WrittenPostBytes += count;
                    }
                    if (p.WrittenPostBytes >= p.TargetPostBytes) completed.Add(p);
                }
                catch (Exception ex)
                {
                    _pending.Remove(p);
                    try { p.Writer.Dispose(); } catch { }
                    TryDelete(p.TempPath);
                    LogService.Write($"[ERROR] Recording failed for {p.Qso.Call}: {ex}");
                    RecordingFailed?.Invoke(p.Qso, ex);
                }
            }
        }
        foreach (var p in completed) QueueCompletion(p);
    }

    private void QueueCompletion(PendingRecording p)
    {
        lock (_sync)
        {
            if (!_pending.Remove(p)) return;
        }

        try { p.Writer.Dispose(); }
        catch (Exception ex)
        {
            TryDelete(p.TempPath);
            LogService.Write($"[ERROR] WAV finalization failed for {p.Qso.Call}: {ex}");
            RecordingFailed?.Invoke(p.Qso, ex);
            return;
        }

        _ = Task.Run(() => EncodeAndComplete(p));
    }

    private void EncodeAndComplete(PendingRecording p)
    {
        try
        {
            using var reader = new WaveFileReader(p.TempPath);
            LogService.Write($"[INFO] Source format: {DescribeFormat(reader.WaveFormat)}");

            ISampleProvider samples = reader.ToSampleProvider();
            var firstChannel = new FirstChannelSampleProvider(samples);
            ISampleProvider resampled = firstChannel.WaveFormat.SampleRate == 24000
                ? firstChannel
                : new WdlResamplingSampleProvider(firstChannel, 24000);
            var normalized = new SampleToWaveProvider16(resampled);

            LogService.Write($"[INFO] Audio normalization: channel 1 -> 24000 Hz, 16-bit PCM, mono");
            LogService.Write($"[INFO] MP3 encoding started: {p.Qso.Call}; bitrate={p.BitrateKbps} kbps");
            using var writer = new LameMP3FileWriter(
                p.FinalMp3Path,
                normalized.WaveFormat,
                new LameConfig { BitRate = p.BitrateKbps });

            var encodeBuffer = new byte[normalized.WaveFormat.AverageBytesPerSecond];
            int bytesRead;
            while ((bytesRead = normalized.Read(encodeBuffer, 0, encodeBuffer.Length)) > 0)
                writer.Write(encodeBuffer, 0, bytesRead);
            TryDelete(p.TempPath);
            LogService.Write($"[INFO] MP3 saved: {p.FinalMp3Path}");
            RecordingCompleted?.Invoke(p.Qso, p.FinalMp3Path);
        }
        catch (Exception mp3Exception)
        {
            TryDelete(p.FinalMp3Path);
            try
            {
                var wavPath = FileNameBuilder.EnsureUnique(p.ContestDirectory, FileNameBuilder.Build(p.Qso, ".wav"));
                File.Move(p.TempPath, wavPath);
                LogService.Write($"[WARNING] MP3 encoding failed for {p.Qso.Call}: {mp3Exception.Message}. WAV saved: {wavPath}");
                Mp3EncodingFailed?.Invoke(p.Qso, wavPath, mp3Exception);
                RecordingCompleted?.Invoke(p.Qso, wavPath);
            }
            catch (Exception fallbackException)
            {
                TryDelete(p.TempPath);
                var combined = new AggregateException("Neither the MP3 file nor the fallback WAV file could be saved.", mp3Exception, fallbackException);
                LogService.Write($"[ERROR] MP3 and WAV fallback failed for {p.Qso.Call}: {combined}");
                RecordingFailed?.Invoke(p.Qso, combined);
            }
        }
    }


    private static string DescribeFormat(WaveFormat format) =>
        $"{format.SampleRate} Hz, {format.BitsPerSample}-bit, {format.Channels} channel(s), {format.Encoding}";

    private sealed class FirstChannelSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _sourceChannels;
        private float[] _sourceBuffer = Array.Empty<float>();

        public FirstChannelSampleProvider(ISampleProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _sourceChannels = source.WaveFormat.Channels;
            if (_sourceChannels < 1)
                throw new InvalidOperationException("The audio source does not contain any channels.");

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;

            var requiredSourceSamples = checked(count * _sourceChannels);
            if (_sourceBuffer.Length < requiredSourceSamples)
                _sourceBuffer = new float[requiredSourceSamples];

            var sourceSamplesRead = _source.Read(_sourceBuffer, 0, requiredSourceSamples);
            var framesRead = sourceSamplesRead / _sourceChannels;

            for (var frame = 0; frame < framesRead; frame++)
                buffer[offset + frame] = _sourceBuffer[frame * _sourceChannels];

            return framesRead;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        _disposed = true;
        _audio.AudioData -= OnAudioData;
        lock (_sync)
        {
            foreach (var p in _pending)
            {
                try { p.Writer.Dispose(); } catch { }
                TryDelete(p.TempPath);
            }
            _pending.Clear();
        }
    }

    private sealed class PendingRecording
    {
        public DxLogQso Qso { get; }
        public string FinalMp3Path { get; }
        public string ContestDirectory { get; }
        public string TempPath { get; }
        public WaveFileWriter Writer { get; }
        public int TargetPostBytes { get; }
        public int BitrateKbps { get; }
        public int WrittenPostBytes { get; set; }

        public PendingRecording(DxLogQso qso, string finalMp3Path, string contestDirectory,
            string tempPath, WaveFileWriter writer, int targetPostBytes, int bitrateKbps)
        {
            Qso = qso;
            FinalMp3Path = finalMp3Path;
            ContestDirectory = contestDirectory;
            TempPath = tempPath;
            Writer = writer;
            TargetPostBytes = targetPostBytes;
            BitrateKbps = bitrateKbps;
        }
    }
}
