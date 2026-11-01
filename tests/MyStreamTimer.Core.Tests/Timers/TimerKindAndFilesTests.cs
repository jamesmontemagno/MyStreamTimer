using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class TimerKindAndFilesTests
{
    [Fact]
    public async Task Countdown_file_preserves_long_pause_resumes_and_resets_saved_duration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mst-lifecycle-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "countdown.txt");
        var store = new InMemorySettingsStore();
        var global = new GlobalSettings(store, directory);
        var settings = new TimerSettings(store, TimerKind.Countdown)
        {
            Minutes = 0, Seconds = 8, UseMinutes = true,
            Output = "{0:mm}:{0:ss}", MakeSound = false,
        };
        var clock = new FakeClock();
        var platform = new FakePlatform();
        var engine = new TimerEngine(settings, global, new ProEntitlement(global),
            new FileOutputService(), platform, clock);
        var completions = 0;
        engine.Completed += (_, _) => Interlocked.Increment(ref completions);
        try
        {
            void Apply(string query) =>
                Assert.True(engine.Apply(UrlCommandParser.Parse($"mystreamtimer://countdown/?{query}")).Applied);

            async Task ExpectFile(string text)
            {
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while ((!File.Exists(path) || File.ReadAllText(path) != text) &&
                       deadline.Elapsed < TimeSpan.FromSeconds(5))
                    await Task.Delay(20);
                Assert.Equal(text, File.ReadAllText(path));
            }

            Apply("secs=12");
            await ExpectFile("00:12");
            clock.Advance(TimeSpan.FromSeconds(3));
            await ExpectFile("00:09");
            Apply("pause");
            Assert.Equal(TimerState.Paused, engine.State);
            clock.Advance(TimeSpan.FromSeconds(20));
            // Give the real scheduler a tick while logical time is beyond the original deadline.
            await Task.Delay(350);
            Assert.Equal("00:09", File.ReadAllText(path));
            Assert.Equal(TimerState.Paused, engine.State);
            Assert.Equal(0, Volatile.Read(ref completions));

            Apply("resume");
            Assert.Equal(TimerState.Running, engine.State);
            await ExpectFile("00:09");
            clock.Advance(TimeSpan.FromSeconds(1));
            await ExpectFile("00:08");
            clock.Advance(TimeSpan.FromSeconds(1));
            await ExpectFile("00:07");

            Apply("reset");
            await ExpectFile("00:08");
            Assert.Equal(TimerState.Running, engine.State);
            clock.Advance(TimeSpan.FromSeconds(1));
            await ExpectFile("00:07");
            Apply("stop");
            await ExpectFile("");
            Assert.Equal(TimerState.Idle, engine.State);
            Assert.False(platform.HasRunningTimers);
            clock.Advance(TimeSpan.FromSeconds(20));
            await engine.DisposeAsync();
            Assert.Equal("", File.ReadAllText(path));
            Assert.Equal(0, Volatile.Read(ref completions));
        }
        finally
        {
            await engine.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

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
