namespace MyStreamTimer.Core.Services;

public interface IClock
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Writes the rendered timer text to disk. Implementations must be cheap to call every 100 ms.</summary>
public interface IFileOutputService
{
    /// <summary>Creates the directory and an empty file if missing (legacy <c>InitializeFile</c>). Never throws.</summary>
    void EnsureFile(string directory, string fileName);

    /// <summary>Creates the directory if needed and returns the full path. May throw.</summary>
    string PrepareTarget(string directory, string fileName);

    /// <summary>Overwrites the file with <paramref name="text"/> (UTF-8, no BOM). May throw.</summary>
    void Write(string fullPath, string text);
}

public sealed class FileOutputService : IFileOutputService
{
    public void EnsureFile(string directory, string fileName)
    {
        try
        {
            var path = PrepareTarget(directory, fileName);
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
        }
        catch
        {
            // legacy swallowed; surfaced later when the timer starts
        }
    }

    public string PrepareTarget(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Writes atomically: the text goes to a sibling temp file which then replaces the target, so a reader
    /// (OBS "read from file") never observes a truncated/empty file mid-write. Falls back to a direct write if the
    /// replace fails (e.g. exotic file systems).
    /// </summary>
    public void Write(string fullPath, string text)
    {
        var tmp = fullPath + ".tmp";
        try
        {
            File.WriteAllText(tmp, text);
            File.Move(tmp, fullPath, overwrite: true);
        }
        catch (Exception) when (File.Exists(tmp))
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
            File.WriteAllText(fullPath, text);
        }
    }
}

/// <summary>Platform hooks the engine needs; everything else lives in the app layer.</summary>
public interface ITimerPlatform
{
    /// <summary>Marks a timer as running (legacy <c>StartActivity</c>) — used for keep-awake and "has running timers".</summary>
    void StartActivity(string id);
    void StopActivity(string id);
    bool HasRunningTimers { get; }

    Task BeepAsync();
}

public sealed class NullTimerPlatform : ITimerPlatform
{
    readonly HashSet<string> active = [];
    public void StartActivity(string id) => active.Add(id);
    public void StopActivity(string id) => active.Remove(id);
    public bool HasRunningTimers => active.Count > 0;
    public Task BeepAsync() => Task.CompletedTask;
}
