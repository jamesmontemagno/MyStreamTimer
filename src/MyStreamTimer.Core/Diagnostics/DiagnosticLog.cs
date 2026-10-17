using System.Diagnostics;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;

namespace MyStreamTimer.Core.Diagnostics;

/// <summary>Bounded local records. Details must be application metadata, never user content or exception messages.</summary>
public sealed class DiagnosticLog
{
    public const int MaximumFiles = 6;
    public const int MaximumFileBytes = 128 * 1024;
    readonly object gate = new();
    readonly int maximumFiles;
    readonly int maximumFileBytes;
    readonly string session = Guid.NewGuid().ToString("N");
    int part;

    public DiagnosticLog(string directoryPath, int maximumFiles = MaximumFiles, int maximumFileBytes = MaximumFileBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileBytes, 1024);
        DirectoryPath = Path.GetFullPath(directoryPath);
        this.maximumFiles = maximumFiles;
        this.maximumFileBytes = maximumFileBytes;
    }

    public string DirectoryPath { get; }

    public void Write(string eventName, string? details = null, Exception? exception = null)
    {
        var entry = new Entry(DateTimeOffset.UtcNow, session, Environment.ProcessId,
            Limit(eventName, 128), Limit(details, 512), exception?.GetType().FullName,
            exception?.HResult, exception is null ? null : Limit(DescribeStack(exception), 4096));
        var bytes = Encode(entry);
        while (bytes.Length > Math.Min(8192, maximumFileBytes))
        {
            entry = entry with { Event = Limit(entry.Event, (entry.Event?.Length ?? 0) / 2),
                Details = Limit(entry.Details, (entry.Details?.Length ?? 0) / 2),
                ExceptionType = Limit(entry.ExceptionType, (entry.ExceptionType?.Length ?? 0) / 2),
                Stack = Limit(entry.Stack, (entry.Stack?.Length ?? 0) / 2) };
            bytes = Encode(entry);
        }
        lock (gate)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = CurrentPath;
            if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > maximumFileBytes)
            {
                part++;
                path = CurrentPath;
            }
            File.AppendAllText(path, Encoding.UTF8.GetString(bytes), new UTF8Encoding(false));
            foreach (var old in Files().Where(file => file.FullName != path).Skip(maximumFiles - 1))
                old.Delete();
        }
    }

    public bool TryExport(string destinationPath, out string? error)
    {
        lock (gate)
        {
            error = null;
            try
            {
                var files = Files().Take(maximumFiles).ToArray();
                if (files.Length == 0)
                {
                    error = "No local diagnostic records are available to export.";
                    return false;
                }
                if (files.Any(file => file.Length > maximumFileBytes))
                {
                    error = "A diagnostic file exceeds the export size limit. The report was not saved.";
                    return false;
                }
                using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var archive = new ZipArchive(output, ZipArchiveMode.Create);
                foreach (var file in files)
                    archive.CreateEntryFromFile(file.FullName, file.Name);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                Trace.TraceError($"Diagnostic export failed: {ex}");
                error = "Couldn't save the report. Choose a writable local folder and try again.";
                return false;
            }
        }
    }

    string CurrentPath => Path.Combine(DirectoryPath, $"diagnostics-{session}-{part:D4}.jsonl");
    IEnumerable<FileInfo> Files() => new DirectoryInfo(DirectoryPath).EnumerateFiles("diagnostics-*.jsonl")
        .OrderByDescending(file => file.LastWriteTimeUtc).ThenByDescending(file => file.Name);
    static string? Limit(string? text, int length) => text?.Length > length ? text[..length] : text;
    static string DescribeStack(Exception exception)
    {
        var text = new StringBuilder();
        Exception? current = exception;
        for (var i = 0; current is not null && i < 8; i++, current = current.InnerException)
        {
            text.AppendLine($"{current.GetType().FullName} (0x{current.HResult:X8})");
            text.AppendLine(new StackTrace(current, false).ToString());
        }
        return text.ToString();
    }

    static byte[] Encode(Entry entry) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + Environment.NewLine);
    sealed record Entry(DateTimeOffset Timestamp, string Session, int ProcessId, string? Event,
        string? Details, string? ExceptionType, int? HResult, string? Stack);
}
