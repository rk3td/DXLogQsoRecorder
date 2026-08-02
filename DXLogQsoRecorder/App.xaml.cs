using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace DXLogQsoRecorder;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static string WriteStartupError(Exception exception)
    {
        var report = new StringBuilder()
            .AppendLine($"UTC: {DateTime.UtcNow:O}")
            .AppendLine($"Application base directory: {AppContext.BaseDirectory}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .ToString();

        var preferredPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        try
        {
            File.WriteAllText(preferredPath, report);
            return preferredPath;
        }
        catch
        {
            var fallbackPath = Path.Combine(Path.GetTempPath(), "DXLogQsoRecorder-startup-error.log");
            try { File.WriteAllText(fallbackPath, report); } catch { }
            return fallbackPath;
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = WriteStartupError(e.Exception);
        MessageBox.Show(
            $"DXLog QSO Recorder could not continue.\n\n{e.Exception.GetBaseException().Message}\n\nDetails were saved to:\n{path}",
            "DXLog QSO Recorder",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(-1);
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception) WriteStartupError(exception);
    }
}
