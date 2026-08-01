using System.IO;
using System.Text.Json;
using DXLogQsoRecorder.Models;

namespace DXLogQsoRecorder.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        PortablePaths.EnsureCreated();
        if (!File.Exists(PortablePaths.SettingsFile)) return new AppSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PortablePaths.SettingsFile), JsonOptions)
                           ?? new AppSettings();

            // Migration from 0.1.x: the old default was 0.0.0.0.
            if (settings.SettingsVersion < 2)
            {
                if (string.IsNullOrWhiteSpace(settings.BindAddress) || settings.BindAddress == "0.0.0.0")
                    settings.BindAddress = "127.0.0.1";
                settings.Mp3BitrateKbps = 48;
                settings.SettingsVersion = 2;
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        PortablePaths.EnsureCreated();
        settings.SettingsVersion = 2;
        File.WriteAllText(PortablePaths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
