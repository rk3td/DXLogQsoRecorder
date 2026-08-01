using System.IO;

namespace DXLogQsoRecorder.Services;

public static class PortablePaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string LogFile => Path.Combine(DataDirectory, "recorder.log");
    public static string PacketDirectory => Path.Combine(DataDirectory, "Packets");
    public static string TempDirectory => Path.Combine(DataDirectory, "Temp");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(PacketDirectory);
        Directory.CreateDirectory(TempDirectory);
    }

    public static void CleanupTempFiles()
    {
        EnsureCreated();
        foreach (var file in Directory.EnumerateFiles(TempDirectory, "*.tmp.wav"))
        {
            try { File.Delete(file); } catch { }
        }
    }
}
