namespace DXLogQsoRecorder.Models;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 2;
    public string BindAddress { get; set; } = "127.0.0.1";
    public int UdpPort { get; set; } = 12060;
    public string? AudioDeviceId { get; set; }
    public int PreBufferSeconds { get; set; } = 30;
    public int PostBufferSeconds { get; set; } = 10;
    public string OutputDirectory { get; set; } = "Recordings";
    public int Mp3BitrateKbps { get; set; } = 48;
    public bool SaveRawPackets { get; set; } = true;
}
