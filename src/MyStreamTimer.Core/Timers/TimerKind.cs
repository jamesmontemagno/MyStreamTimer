namespace MyStreamTimer.Core.Timers;

/// <summary>
/// Timer identifiers. The string ids are persisted as settings-key suffixes and output file names,
/// so they must stay byte-identical to the legacy app (<c>Constants.cs</c>).
/// </summary>
public enum TimerKind
{
    Countdown,
    Countdown2,
    Countdown3,
    Countdown4,
    Countup,
    Countup2,
    Time,
}

public static class TimerKindExtensions
{
    public static readonly IReadOnlyList<TimerKind> All =
    [
        TimerKind.Countdown, TimerKind.Countdown2, TimerKind.Countdown3, TimerKind.Countdown4,
        TimerKind.Countup, TimerKind.Countup2, TimerKind.Time,
    ];

    /// <summary>Legacy id — settings key suffix and default file name stem.</summary>
    public static string Id(this TimerKind kind) => kind switch
    {
        TimerKind.Countdown => "countdown",
        TimerKind.Countdown2 => "countdown2",
        TimerKind.Countdown3 => "countdown3",
        TimerKind.Countdown4 => "countdown4",
        TimerKind.Countup => "countup",
        TimerKind.Countup2 => "countup2",
        TimerKind.Time => "time",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Title(this TimerKind kind) => kind switch
    {
        TimerKind.Countdown => "Countdown 1",
        TimerKind.Countdown2 => "Countdown 2",
        TimerKind.Countdown3 => "Countdown 3",
        TimerKind.Countdown4 => "Countdown 4",
        TimerKind.Countup => "Count Up 1",
        TimerKind.Countup2 => "Count Up 2",
        TimerKind.Time => "Current Time",
        _ => kind.ToString(),
    };

    public static string ShortTitle(this TimerKind kind) => kind switch
    {
        TimerKind.Countdown => "Down",
        TimerKind.Countdown2 => "Down 2",
        TimerKind.Countdown3 => "Down 3",
        TimerKind.Countdown4 => "Down 4",
        TimerKind.Countup => "Up",
        TimerKind.Countup2 => "Up 2",
        TimerKind.Time => "Time",
        _ => kind.ToString(),
    };

    public static bool IsCountdown(this TimerKind kind) =>
        kind is TimerKind.Countdown or TimerKind.Countdown2 or TimerKind.Countdown3 or TimerKind.Countdown4;

    public static bool IsCountUp(this TimerKind kind) => kind is TimerKind.Countup or TimerKind.Countup2;

    public static bool IsTime(this TimerKind kind) => kind == TimerKind.Time;

    /// <summary>Legacy Pro gating: Countdown 4, Count Up 2 and Time require Pro.</summary>
    public static bool RequiresPro(this TimerKind kind) =>
        kind is TimerKind.Countdown4 or TimerKind.Countup2 or TimerKind.Time;

    public static int DefaultMinutes(this TimerKind kind) => kind.IsCountdown() ? 5 : 0;

    public static string DefaultOutput(this TimerKind kind) =>
        kind.IsCountUp() ? @"{0:hh\:mm\:ss}" : @"Starting in {0:hh\:mm\:ss}";

    public const string DefaultFinishText = "Let's do this!";

    public static string DefaultFileName(this TimerKind kind) => $"{kind.Id()}.txt";

    /// <summary>Default Segoe Fluent Icons glyph: Stopwatch / Up / Recent(clock).</summary>
    public static string DefaultIconGlyph(this TimerKind kind) =>
        kind.IsCountdown() ? "\uE916" : kind.IsCountUp() ? "\uE74A" : "\uE823";

    /// <summary>Curated icon choices offered in Settings (glyph, label).</summary>
    public static readonly IReadOnlyList<(string Glyph, string Label)> IconChoices =
    [
        ("\uE916", "Stopwatch"), ("\uE74A", "Up"), ("\uE823", "Clock"), ("\uE945", "Lightning"), ("\uE735", "Star"),
        ("\uE8D4", "Trophy"), ("\uE7C8", "Gift"), ("\uE7F4", "Game"), ("\uE8B8", "Music"), ("\uE714", "Video"),
        ("\uE8A1", "Megaphone"), ("\uE7EE", "Coffee"), ("\uE8C9", "Rocket"), ("\uE734", "Favorite"), ("\uE8EC", "Flag"),
        ("\uE720", "Microphone"), ("\uE7FC", "Monitor"), ("\uE8BD", "Chat"), ("\uE787", "Calendar"), ("\uE8F1", "Book"),
    ];

    public static IReadOnlyList<string> OutputStyleOptions(this TimerKind kind) => kind.IsTime()
        ? ["Hour:Minute (9:10)", "Hour:Minute:Second (9:10:05)", "Hour:Minute (24-hour) (19:10)", "Hour:Minute:Second (24-hour) (19:10:05)"]
        : ["Custom", "Auto", "Total Seconds (120)", "Total Minutes:Seconds (90:00)"];

    /// <summary>Maps a <c>mystreamtimer://</c> URL host to a timer. Returns null for unknown hosts.</summary>
    public static TimerKind? FromHost(string? host) => host?.ToLowerInvariant() switch
    {
        "countdown" or "countdown1" => TimerKind.Countdown,
        "countdown2" => TimerKind.Countdown2,
        "countdown3" => TimerKind.Countdown3,
        "countdown4" => TimerKind.Countdown4,
        "countup" or "countup1" => TimerKind.Countup,
        "countup2" => TimerKind.Countup2,
        "time" => TimerKind.Time,
        _ => null,
    };
}
