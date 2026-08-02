using System.Globalization;
using System.IO;
using DXLogQsoRecorder.Models;
using Microsoft.Data.Sqlite;
using NAudio.Wave;

namespace DXLogQsoRecorder.Services;

public sealed class RecordingIndexService
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(PortablePaths.DataDirectory, "recordings.db"),
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    public RecordingIndexService()
    {
        // The SQLite database lives in the portable Data directory.
        // Ensure that directory exists before opening the database file.
        PortablePaths.EnsureCreated();
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Recordings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                MyCall TEXT NOT NULL,
                Callsign TEXT NOT NULL,
                Contest TEXT NOT NULL,
                Band TEXT NOT NULL,
                Mode TEXT NOT NULL,
                Format TEXT NOT NULL,
                DurationSeconds REAL NOT NULL,
                FilePath TEXT NOT NULL UNIQUE COLLATE NOCASE,
                FileSize INTEGER NOT NULL,
                LastModifiedUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Recordings_Callsign ON Recordings(Callsign COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Recordings_Contest ON Recordings(Contest COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Recordings_Timestamp ON Recordings(Timestamp DESC);
            """;
        command.ExecuteNonQuery();
    }

    public async Task SynchronizeAsync(string recordingsRoot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(recordingsRoot);
        var files = Directory.EnumerateFiles(recordingsRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsAudioFile).ToArray();
        var existing = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = ParseFile(file);
            if (item is not null) await UpsertInternalAsync(connection, transaction, item, cancellationToken);
        }

        await using (var select = connection.CreateCommand())
        {
            select.Transaction = (SqliteTransaction)transaction;
            select.CommandText = "SELECT FilePath FROM Recordings";
            var stale = new List<string>();
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var path = reader.GetString(0);
                if (!existing.Contains(Path.GetFullPath(path))) stale.Add(path);
            }
            await reader.CloseAsync();
            foreach (var path in stale)
            {
                await using var delete = connection.CreateCommand();
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM Recordings WHERE FilePath=$path";
                delete.Parameters.AddWithValue("$path", path);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpsertAsync(string filePath, DxLogQso? qso = null)
    {
        var item = qso is null ? ParseFile(filePath) : CreateFromQso(filePath, qso);
        if (item is null) return;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await UpsertInternalAsync(connection, transaction, item, CancellationToken.None);
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<RecordingBrowserItem>> SearchAsync(string? callsign, string? contest)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Timestamp, MyCall, Callsign, Contest, Band, Mode, Format,
                   DurationSeconds, FileSize, FilePath
            FROM Recordings
            WHERE ($call='' OR Callsign LIKE '%' || $call || '%' COLLATE NOCASE)
              AND ($contest='' OR Contest=$contest COLLATE NOCASE)
            ORDER BY Timestamp DESC
            LIMIT 10000
            """;
        command.Parameters.AddWithValue("$call", (callsign ?? "").Trim().Replace('/', '_'));
        command.Parameters.AddWithValue("$contest", contest is null || contest == "All contests" ? "" : contest);
        var result = new List<RecordingBrowserItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new RecordingBrowserItem
            {
                Id = reader.GetInt64(0), Timestamp = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                MyCall = reader.GetString(2), Callsign = reader.GetString(3), Contest = reader.GetString(4),
                Band = reader.GetString(5), Mode = reader.GetString(6), Format = reader.GetString(7),
                Duration = TimeSpan.FromSeconds(reader.GetDouble(8)), FileSize = reader.GetInt64(9), FilePath = reader.GetString(10)
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<string>> GetContestsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Contest FROM Recordings ORDER BY Contest COLLATE NOCASE";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task UpsertInternalAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, RecordingBrowserItem item, CancellationToken token)
    {
        var info = new FileInfo(item.FilePath);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO Recordings(Timestamp,MyCall,Callsign,Contest,Band,Mode,Format,DurationSeconds,FilePath,FileSize,LastModifiedUtc)
            VALUES($timestamp,$mycall,$call,$contest,$band,$mode,$format,$duration,$path,$size,$modified)
            ON CONFLICT(FilePath) DO UPDATE SET
              Timestamp=excluded.Timestamp, MyCall=excluded.MyCall, Callsign=excluded.Callsign,
              Contest=excluded.Contest, Band=excluded.Band, Mode=excluded.Mode, Format=excluded.Format,
              DurationSeconds=excluded.DurationSeconds, FileSize=excluded.FileSize, LastModifiedUtc=excluded.LastModifiedUtc
            """;
        command.Parameters.AddWithValue("$timestamp", item.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$mycall", item.MyCall);
        command.Parameters.AddWithValue("$call", item.Callsign);
        command.Parameters.AddWithValue("$contest", item.Contest);
        command.Parameters.AddWithValue("$band", item.Band);
        command.Parameters.AddWithValue("$mode", item.Mode);
        command.Parameters.AddWithValue("$format", item.Format);
        command.Parameters.AddWithValue("$duration", item.Duration.TotalSeconds);
        command.Parameters.AddWithValue("$path", Path.GetFullPath(item.FilePath));
        command.Parameters.AddWithValue("$size", info.Exists ? info.Length : item.FileSize);
        command.Parameters.AddWithValue("$modified", info.Exists ? info.LastWriteTimeUtc.ToString("O") : DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);
    }

    private static RecordingBrowserItem CreateFromQso(string path, DxLogQso qso)
    {
        var info = new FileInfo(path);
        return new RecordingBrowserItem
        {
            Timestamp=qso.Timestamp, MyCall=qso.MyCall, Callsign=qso.Call, Contest=string.IsNullOrWhiteSpace(qso.ContestName)?"Unknown":qso.ContestName,
            Band=qso.Band+" MHz", Mode=qso.Mode, Format=Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
            Duration=TryDuration(path), FileSize=info.Exists?info.Length:0, FilePath=Path.GetFullPath(path)
        };
    }

    private static RecordingBrowserItem? ParseFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var contest = info.Directory?.Name ?? "Unknown";
            var parts = Path.GetFileNameWithoutExtension(path).Split('_');
            if (parts.Length < 6 || !DateTime.TryParseExact(parts[0]+parts[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)) return null;
            var myCall = parts[2];
            var band = parts[^2];
            var mode = parts[^1];
            var call = string.Join('_', parts.Skip(3).Take(parts.Length-5));
            return new RecordingBrowserItem
            {
                Timestamp=timestamp, MyCall=myCall, Callsign=call, Contest=contest, Band=band, Mode=mode,
                Format=info.Extension.TrimStart('.').ToUpperInvariant(), Duration=TryDuration(path), FileSize=info.Length, FilePath=info.FullName
            };
        }
        catch (Exception ex) { LogService.Write($"[WARNING] Could not index {path}: {ex.Message}"); return null; }
    }

    private static TimeSpan TryDuration(string path)
    {
        try { using var reader = new AudioFileReader(path); return reader.TotalTime; }
        catch { return TimeSpan.Zero; }
    }

    private static bool IsAudioFile(string path) => Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);
}
