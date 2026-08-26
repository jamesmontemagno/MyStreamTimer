using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class TimerKindAndFilesTests
{
    [Fact]
    public void Ids_titles_and_gating_match_legacy()
    {
        Assert.Equal(["countdown", "countdown2", "countdown3", "countdown4", "countup", "countup2", "time"], TimerKindExtensions.All.Select(k => k.Id()));
        Assert.Equal("countdown4.txt", TimerKind.Countdown4.DefaultFileName());
        Assert.True(TimerKind.Countdown4.RequiresPro());
        Assert.True(TimerKind.Countup2.RequiresPro());
        Assert.True(TimerKind.Time.RequiresPro());
        Assert.False(TimerKind.Countdown.RequiresPro());
        Assert.Equal("Countdown 1", TimerKind.Countdown.Title());
        Assert.Equal("Down 2", TimerKind.Countdown2.ShortTitle());
        Assert.Equal("Current Time", TimerKind.Time.Title());
        Assert.Equal(4, TimerKind.Time.OutputStyleOptions().Count);
        Assert.Equal("Custom", TimerKind.Countup.OutputStyleOptions()[0]);
        Assert.Equal(TimerKind.Countdown, TimerKindExtensions.FromHost("COUNTDOWN1"));
        Assert.Null(TimerKindExtensions.FromHost("giveaway"));
        Assert.Null(TimerKindExtensions.FromHost(null));
    }

    [Fact]
    public void FileOutputService_creates_and_writes_utf8_without_bom()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mst-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var svc = new FileOutputService();
            svc.EnsureFile(dir, "countdown.txt");
            var path = Path.Combine(dir, "countdown.txt");
            Assert.True(File.Exists(path));
            Assert.Equal("", File.ReadAllText(path));

            var target = svc.PrepareTarget(dir, "countdown.txt");
            Assert.Equal(path, target);
            svc.Write(target, "Starting in 00:00:05");
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "must not write a BOM");
            Assert.Equal("Starting in 00:00:05", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
