namespace DXLogQsoRecorder.Models;

public sealed class RecordingBrowserItem
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string MyCall { get; init; } = "";
    public string Callsign { get; init; } = "";
    public string Contest { get; init; } = "";
    public string Band { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Format { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public long FileSize { get; init; }
    public string FilePath { get; init; } = "";
    public string DurationText => Duration <= TimeSpan.Zero ? "—" : Duration.ToString(@"mm\:ss");
    public string SizeText => FileSize < 1024 * 1024 ? $"{FileSize / 1024.0:0} KB" : $"{FileSize / 1024.0 / 1024.0:0.0} MB";
}
