using System.Globalization;
using System.Text.RegularExpressions;

namespace MyStreamTimer.Core.Timers;

/// <summary>
/// Renders timer text exactly like the legacy <c>TimerViewModel.UpdateTimer</c> loop.
/// </summary>
public static partial class OutputFormatter
{
    public const string InvalidFormatMessage = @"Invalid time format. Use {0:hh\:mm\:ss}";

    /// <summary>Countdown / count-up styles: 0 custom, 1 auto, 2 total seconds, 3 total M:ss.</summary>
    public static string FormatElapsed(TimeSpan elapsed, int style, string customTemplate)
    {
        switch (style)
        {
            case 1:
                return FormatAuto(elapsed);
            case 2:
                return Math.Floor(elapsed.TotalSeconds).ToString("N0");
            case 3:
                return $"{Math.Floor(elapsed.TotalMinutes):N0}:{string.Format("{0:ss}", elapsed)}";
            default:
                return string.Format(NormalizeTemplate(customTemplate), elapsed);
        }
    }

    static string FormatAuto(TimeSpan t)
    {
        if (Math.Floor(t.TotalDays) > 0)
        {
            if (t.TotalDays >= 10000) return string.Format("{0:ddddd\\:hh\\:mm\\:ss}", t);
            if (t.TotalDays >= 1000) return string.Format("{0:dddd\\:hh\\:mm\\:ss}", t);
            if (t.TotalDays >= 100) return string.Format("{0:ddd\\:hh\\:mm\\:ss}", t);
            if (t.TotalDays >= 10) return string.Format("{0:dd\\:hh\\:mm\\:ss}", t);
            return string.Format("{0:d\\:hh\\:mm\\:ss}", t);
        }
        if (Math.Floor(t.TotalHours) > 0)
            return t.TotalHours >= 10 ? string.Format("{0:hh\\:mm\\:ss}", t) : string.Format("{0:h\\:mm\\:ss}", t);
        if (Math.Floor(t.TotalMinutes) > 0)
            return t.TotalMinutes >= 10 ? string.Format("{0:mm\\:ss}", t) : string.Format("{0:m\\:ss}", t);
        return t.TotalSeconds >= 10 ? string.Format("{0:ss}", t) : Math.Floor(t.TotalSeconds).ToString("N0");
    }

    /// <summary>Clock styles: 0 h:mm, 1 h:mm:ss, 2 H:mm, 3 H:mm:ss; AM/PM appends " tt".</summary>
    public static string FormatTime(DateTime now, int style, bool showAmPm)
    {
        var pattern = style switch
        {
            1 => "h:mm:ss",
            2 => "H:mm",
            3 => "H:mm:ss",
            _ => "h:mm",
        };
        if (showAmPm)
            pattern += " tt";
        return now.ToString(pattern);
    }

    /// <summary>True when the clock style only shows minutes (legacy only re-rendered once a minute).</summary>
    public static bool TimeStyleIsMinuteResolution(int style) => style is 0 or 2;

    /// <summary>Validates a custom template the same way legacy did (format a 5 s span).</summary>
    public static bool IsValidTemplate(string template)
    {
        try
        {
            _ = string.Format(NormalizeTemplate(template), TimeSpan.FromSeconds(5));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Users coming from the Swift app (and many new users) type <c>{0:hh:mm:ss}</c>; .NET requires the colons
    /// inside a TimeSpan format to be escaped. Insert the escapes inside <c>{0:...}</c> blocks when missing.
    /// </summary>
    public static string NormalizeTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return TemplateBlock().Replace(template, m =>
        {
            var inner = m.Groups[1].Value;
            if (inner.Contains("\\:"))
                return m.Value;
            return "{0:" + inner.Replace(":", "\\:") + "}";
        });
    }

    [GeneratedRegex(@"\{0:([^}]*)\}")]
    private static partial Regex TemplateBlock();
}
