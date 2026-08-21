using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Automation;

public enum CommandAction
{
    Start,
    Stop,
    Add,
    Subtract,
    Pause,
    Resume,
    Reset,
    None,
}

/// <summary>Result of parsing a <c>mystreamtimer://</c> URL.</summary>
/// <param name="Action">What to do; <see cref="CommandAction.None"/> if the URL was not understood.</param>
/// <param name="Minutes">Minutes for Start/Add/Subtract (fractional allowed); -1 when not applicable.</param>
/// <param name="Host">Lower-cased URL host (e.g. <c>countdown2</c>), empty if invalid.</param>
public readonly record struct UrlCommand(CommandAction Action, float Minutes, string Host)
{
    public static readonly UrlCommand None = new(CommandAction.None, -1, string.Empty);

    public TimerKind? Kind => TimerKindExtensions.FromHost(Host);

    public bool IsValid => Action != CommandAction.None && Kind is not null;
}

/// <summary>
/// Byte-for-byte port of the legacy <c>Utils.ParseStartupArgs</c>. Stream Deck buttons, OBS scripts and
/// user shortcuts depend on this exact grammar (host list, query precedence, lower-casing, <c>Remove(0,n)</c>
/// value extraction, <c>topofhour</c> formula and midnight wrap for <c>to=</c>).
/// The only additions are the <c>time</c> host and an injectable clock.
/// </summary>
public static class UrlCommandParser
{
    static readonly string[] LegacyHosts =
    [
        "countdown", "countdown1", "countdown2", "countdown3", "countdown4",
        "countup", "countup1", "countup2",
    ];

    public static UrlCommand Parse(string? args) => Parse(args, DateTime.Now);

    public static UrlCommand Parse(string? args, DateTime now)
    {
        var action = CommandAction.None;
        float mins = -1;
        var host = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(args))
                return new UrlCommand(action, mins, host);

            var uri = new Uri(args);
            host = uri.Host.ToLowerInvariant();
            var query = uri.Query.ToLowerInvariant();

            if (!LegacyHosts.Contains(host) && host != "time")
                return new UrlCommand(action, mins, string.Empty);

            if (query.Contains("?mins=") && float.TryParse(query.Remove(0, 6), out mins))
            {
                if (mins > 0)
                    action = CommandAction.Start;
            }
            else if (query.Contains("?secs=") && float.TryParse(query.Remove(0, 6), out var secs))
            {
                mins = secs / 60.0f;
                if (mins > 0)
                    action = CommandAction.Start;
            }
            else if (query.Contains("?topofhour"))
            {
                mins = 60.0f - now.Minute;
                mins += (60.0f - now.Second) / 60.0f;
                mins -= 1;
                if (mins < 0)
                    mins = 0;

                if (mins > 0)
                    action = CommandAction.Start;
            }
            else if (query.Contains("?to=") && DateTime.TryParse(query.Remove(0, 4), out var date))
            {
                if (date.TimeOfDay > now.TimeOfDay)
                    mins = (float)(date.TimeOfDay.TotalMinutes - now.TimeOfDay.TotalMinutes);
                else
                {
                    // time until midnight, then until target
                    mins = (float)(1440.0 - now.TimeOfDay.TotalMinutes);
                    mins += (float)date.TimeOfDay.TotalMinutes;
                }
                if (mins > 0)
                    action = CommandAction.Start;
            }
            else if (query.Contains("?addmins=") && float.TryParse(query.Remove(0, 9), out mins))
            {
                if (mins > 0)
                    action = CommandAction.Add;
            }
            else if (query.Contains("?addsecs=") && float.TryParse(query.Remove(0, 9), out var addsecs))
            {
                mins = addsecs / 60.0f;
                if (mins > 0)
                    action = CommandAction.Add;
            }
            else if (query.Contains("?subtractmins=") && float.TryParse(query.Remove(0, 14), out mins))
            {
                if (mins > 0)
                    action = CommandAction.Subtract;
            }
            else if (query.Contains("?subtractsecs=") && float.TryParse(query.Remove(0, 14), out var subsecs))
            {
                mins = subsecs / 60.0f;
                if (mins > 0)
                    action = CommandAction.Subtract;
            }
            else if (query.Contains("?pause"))
                action = CommandAction.Pause;
            else if (query.Contains("?resume"))
                action = CommandAction.Resume;
            else if (query.Contains("?reset"))
                action = CommandAction.Reset;
            else if (query.Contains("?stop"))
                action = CommandAction.Stop;
            else if (host == "time" && query.Contains("?start"))
                action = CommandAction.Start; // new: clock has no duration
        }
        catch
        {
            // legacy swallowed everything and returned whatever was parsed so far
        }

        return new UrlCommand(action, mins, host);
    }

    /// <summary>Builds a URL for the Automation command builder.</summary>
    public static string Build(TimerKind kind, CommandAction action, double? value = null, bool valueIsSeconds = false, string? clockTime = null, bool topOfHour = false)
    {
        var host = kind.Id();

        // The legacy parser uses culture-sensitive float.TryParse, so decimals do not round-trip on comma-decimal
        // locales. Emit whole numbers only: fractional minutes are converted to whole seconds.
        var v = Math.Max(0, value ?? 0);
        if (!valueIsSeconds && Math.Abs(v - Math.Round(v)) > 0.0001)
        {
            v = Math.Round(v * 60);
            valueIsSeconds = true;
        }
        var n = ((long)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var query = action switch
        {
            CommandAction.Start when topOfHour => "?topofhour",
            CommandAction.Start when clockTime is not null => $"?to={clockTime}",
            CommandAction.Start when kind.IsTime() => "?start",
            CommandAction.Start => valueIsSeconds ? $"?secs={n}" : $"?mins={n}",
            CommandAction.Add => valueIsSeconds ? $"?addsecs={n}" : $"?addmins={n}",
            CommandAction.Subtract => valueIsSeconds ? $"?subtractsecs={n}" : $"?subtractmins={n}",
            CommandAction.Pause => "?pause",
            CommandAction.Resume => "?resume",
            CommandAction.Reset => "?reset",
            CommandAction.Stop => "?stop",
            _ => string.Empty,
        };
        return $"mystreamtimer://{host}/{query}";
    }
}
