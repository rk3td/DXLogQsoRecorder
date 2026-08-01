namespace DXLogQsoRecorder.Models;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public override string ToString() => Name;
}
