using System.IO;

namespace DXLogQsoRecorder.Services;

public static class LogService
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        try
        {
            PortablePaths.EnsureCreated();
            lock (Sync)
            {
                File.AppendAllText(PortablePaths.LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
