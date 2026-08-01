namespace DXLogQsoRecorder.Models;

public sealed class RecordingItem
{
    public required string QsoKey { get; init; }
    public DateTime ReceivedAt { get; init; }
    public required string Call { get; init; }
    public required string Band { get; init; }
    public required string Mode { get; init; }
    public required string Status { get; set; }
    public string FileName { get; set; } = "";
}
