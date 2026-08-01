namespace DXLogQsoRecorder.Models;

public sealed class DxLogQso
{
    public string Logger { get; init; } = "";
    public string QsoId { get; init; } = "";
    public string ContestName { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string MyCall { get; init; } = "";
    public string Call { get; init; } = "";
    public string Band { get; init; } = "";
    public string Mode { get; init; } = "";
    public string TxFrequency { get; init; } = "";
    public string RxFrequency { get; init; } = "";
    public string StationId { get; init; } = "";
    public string Guid { get; init; } = "";
    public bool IsNewQso { get; init; }
}
