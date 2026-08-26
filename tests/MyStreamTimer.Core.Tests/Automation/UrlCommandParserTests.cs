using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Tests.LegacyOracle;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Automation;

public class UrlCommandParserTests
{
    public static IEnumerable<object[]> LegacyUrls()
    {
        var hosts = new[] { "countdown", "countdown1", "countdown2", "countdown3", "countdown4", "countup", "countup1", "countup2", "COUNTDOWN2", "giveaway", "bogus" };
        var queries = new[]
        {
            "", "?mins=6", "?mins=0", "?mins=-3", "?mins=1.5", "?mins=abc", "?secs=90", "?secs=0", "?topofhour", "?to=15:30", "?to=00:01", "?to=23:59", "?to=garbage",
            "?addmins=1", "?addmins=0", "?addsecs=90", "?subtractmins=2", "?subtractsecs=30", "?pause", "?resume", "?reset", "?stop", "?PAUSE", "?mins=5&foo=bar", "?foo=bar",
        };
        foreach (var h in hosts)
            foreach (var q in queries)
                yield return [$"mystreamtimer://{h}/{q}"];

        yield return ["not a url"];
        yield return ["mystreamtimer://countdown/?mins=6"];
        yield return ["mystreamtimer://countdown?mins=6"];
        yield return [""];
    }

    [Theory]
    [MemberData(nameof(LegacyUrls))]
    public void Matches_legacy_parser(string url)
    {
        // Both implementations read DateTime.Now for topofhour/to=; run them back-to-back so the minute is the same.
        var now = DateTime.Now;
        var (action, mins, host) = LegacyUtils.ParseStartupArgs(url);
        var result = UrlCommandParser.Parse(url, now);

        Assert.Equal(action.ToString(), result.Action.ToString());
        Assert.Equal(host, result.Host);
        if (action != LegacyUtils.CommandAction.None && url.Contains("?to=") == false && url.Contains("?topofhour") == false)
            Assert.Equal(mins, result.Minutes, 3);
        else if (action != LegacyUtils.CommandAction.None)
            Assert.InRange(Math.Abs(mins - result.Minutes), 0, 0.1f); // time-dependent: allow a few seconds drift
    }

    [Fact]
    public void Time_host_is_new_and_supported()
    {
        var r = UrlCommandParser.Parse("mystreamtimer://time/?start");
        Assert.Equal(CommandAction.Start, r.Action);
        Assert.Equal(TimerKind.Time, r.Kind);

        var s = UrlCommandParser.Parse("mystreamtimer://time/?stop");
        Assert.Equal(CommandAction.Stop, s.Action);
    }

    [Fact]
    public void To_wraps_past_midnight()
    {
        var now = new DateTime(2026, 1, 1, 23, 0, 0);
        var r = UrlCommandParser.Parse("mystreamtimer://countdown/?to=01:00", now);
        Assert.Equal(CommandAction.Start, r.Action);
        Assert.Equal(120f, r.Minutes, 2);
    }

    [Fact]
    public void TopOfHour_formula_matches_legacy()
    {
        var now = new DateTime(2026, 1, 1, 10, 30, 30);
        var r = UrlCommandParser.Parse("mystreamtimer://countdown/?topofhour", now);
        var expected = 60f - 30 + (60f - 30) / 60f - 1;
        Assert.Equal(expected, r.Minutes, 3);
    }

    [Theory]
    [InlineData(TimerKind.Countdown, CommandAction.Start, 5.0, false, "mystreamtimer://countdown/?mins=5")]
    [InlineData(TimerKind.Countdown2, CommandAction.Start, 90.0, true, "mystreamtimer://countdown2/?secs=90")]
    [InlineData(TimerKind.Countup, CommandAction.Add, 1.0, false, "mystreamtimer://countup/?addmins=1")]
    [InlineData(TimerKind.Countup2, CommandAction.Subtract, 30.0, true, "mystreamtimer://countup2/?subtractsecs=30")]
    [InlineData(TimerKind.Countdown3, CommandAction.Pause, null, false, "mystreamtimer://countdown3/?pause")]
    [InlineData(TimerKind.Countdown4, CommandAction.Stop, null, false, "mystreamtimer://countdown4/?stop")]
    [InlineData(TimerKind.Time, CommandAction.Start, null, false, "mystreamtimer://time/?start")]
    public void Build_roundtrips_through_Parse(TimerKind kind, CommandAction action, double? value, bool secs, string expected)
    {
        var url = UrlCommandParser.Build(kind, action, value, secs);
        Assert.Equal(expected, url);
        var parsed = UrlCommandParser.Parse(url);
        Assert.Equal(action, parsed.Action);
        Assert.Equal(kind, parsed.Kind);
    }
}

