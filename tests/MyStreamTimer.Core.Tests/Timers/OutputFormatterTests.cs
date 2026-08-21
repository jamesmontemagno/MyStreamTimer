using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class OutputFormatterTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "9")]
    [InlineData(10, "10")]
    [InlineData(59, "59")]
    [InlineData(60, "1:00")]
    [InlineData(599, "9:59")]
    [InlineData(600, "10:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(35999, "9:59:59")]
    [InlineData(36000, "10:00:00")]
    [InlineData(86400, "1:00:00:00")]
    [InlineData(864000, "10:00:00:00")]
    [InlineData(8640000, "100:00:00:00")]
    public void Auto_style_matches_legacy_ladder(int seconds, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatElapsed(TimeSpan.FromSeconds(seconds), 1, ""));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(120, "120")]
    [InlineData(5400, "5,400")] // legacy used N0 => thousands separators
    public void TotalSeconds_style(int seconds, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatElapsed(TimeSpan.FromSeconds(seconds), 2, ""));

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5400, "90:00")]
    [InlineData(65, "1:05")]
    public void TotalMinutesSeconds_style(int seconds, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatElapsed(TimeSpan.FromSeconds(seconds), 3, ""));

    [Theory]
    [InlineData(@"Starting in {0:hh\:mm\:ss}", 3661, "Starting in 01:01:01")]
    [InlineData(@"{0:hh\:mm\:ss}", 5, "00:00:05")]
    [InlineData("{0:hh:mm:ss}", 5, "00:00:05")] // Swift-style unescaped template is accepted
    [InlineData("Giveaway in {0:mm:ss}", 125, "Giveaway in 02:05")]
    [InlineData("plain text", 5, "plain text")]
    public void Custom_style(string template, int seconds, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatElapsed(TimeSpan.FromSeconds(seconds), 0, template));

    [Fact]
    public void Invalid_template_detected()
    {
        Assert.False(OutputFormatter.IsValidTemplate("{0:zz}"));
        Assert.False(OutputFormatter.IsValidTemplate("{1}"));
        Assert.True(OutputFormatter.IsValidTemplate(@"{0:hh\:mm\:ss}"));
        Assert.True(OutputFormatter.IsValidTemplate("{0:hh:mm:ss}"));
    }

    [Fact]
    public void Normalize_leaves_escaped_templates_alone()
    {
        const string t = @"Starting in {0:hh\:mm\:ss}";
        Assert.Equal(t, OutputFormatter.NormalizeTemplate(t));
        Assert.Equal(@"{0:hh\:mm}", OutputFormatter.NormalizeTemplate("{0:hh:mm}"));
    }

    [Theory]
    [InlineData(0, false, "9:10")]
    [InlineData(0, true, "9:10 PM")]
    [InlineData(1, false, "9:10:05")]
    [InlineData(1, true, "9:10:05 PM")]
    [InlineData(2, false, "21:10")]
    [InlineData(2, true, "21:10 PM")]
    [InlineData(3, false, "21:10:05")]
    [InlineData(3, true, "21:10:05 PM")]
    public void Time_styles(int style, bool ampm, string expected)
    {
        var prev = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, OutputFormatter.FormatTime(new DateTime(2026, 1, 1, 21, 10, 5), style, ampm));
        }
        finally { Thread.CurrentThread.CurrentCulture = prev; }
    }
}

