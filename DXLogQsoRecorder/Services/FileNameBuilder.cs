using System.IO;
using DXLogQsoRecorder.Models;

namespace DXLogQsoRecorder.Services;

public static class FileNameBuilder
{
    public static string Build(DxLogQso qso, string extension = ".mp3")
    {
        if (!extension.StartsWith('.')) extension = "." + extension;
        var name = $"{qso.Timestamp:yyyyMMdd_HHmmss}_{qso.MyCall}_{qso.Call}_{qso.Band}MHz_{qso.Mode}{extension}";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    public static string BuildContestDirectory(string outputRoot, string? contestName)
    {
        var folderName = string.IsNullOrWhiteSpace(contestName) ? "Unknown" : contestName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) folderName = folderName.Replace(c, '_');
        folderName = folderName.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(folderName)) folderName = "Unknown";
        return Path.Combine(outputRoot, folderName);
    }

    public static string EnsureUnique(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; ; i++)
        {
            path = Path.Combine(directory, $"{stem}_{i}{ext}");
            if (!File.Exists(path)) return path;
        }
    }
}
