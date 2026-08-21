using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0);
    public DateTime UtcNow => Now.ToUniversalTime();
    public void Advance(TimeSpan by) => Now += by;
}

sealed class FakeFiles : IFileOutputService
{
    public readonly List<string> Writes = [];
    public string? LastPath;
    public bool FailWrites;
    public void EnsureFile(string directory, string fileName) { }
    public string PrepareTarget(string directory, string fileName) => LastPath = Path.Combine(directory, fileName);
    public void Write(string fullPath, string text)
    {
        if (FailWrites) throw new IOException("denied");
        lock (Writes) Writes.Add(text);
    }
    public string Last { get { lock (Writes) return Writes.Count == 0 ? "" : Writes[^1]; } }
}

sealed class FakePlatform : ITimerPlatform
{
    public int Beeps;
    readonly HashSet<string> active = [];
    public void StartActivity(string id) => active.Add(id);
    public void StopActivity(string id) => active.Remove(id);
    public bool HasRunningTimers => active.Count > 0;
    public Task BeepAsync() { Beeps++; return Task.CompletedTask; }
}

public class TimerEngineTests
{
    static (TimerEngine engine, FakeClock clock, FakeFiles files, FakePlatform platform, TimerSettings settings) Create(TimerKind kind, Action<TimerSettings>? configure = null)
    {
        var store = new InMemorySettingsStore();
        var global = new GlobalSettings(store, @"C:\out");
        var settings = new TimerSettings(store, kind);
        configure?.Invoke(settings);
        var clock = new FakeClock();
        var files = new FakeFiles();
        var platform = new FakePlatform();
        var engine = new TimerEngine(settings, global, new ProEntitlement(global), files, platform, clock);
        return (engine, clock, files, platform, settings);
    }

    static async Task WaitFor(Func<bool> cond, int ms = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond() && sw.ElapsedMilliseconds < ms)
            await Task.Delay(20);
        Assert.True(cond(), "condition not met in time");
    }

    [Fact]
    public async Task Countdown_writes_text_then_finish_and_beeps()
    {
        var (e, clock, files, platform, _) = Create(TimerKind.Countdown, s => { s.Minutes = 0; s.Seconds = 3; s.MakeSound = true; s.Finish = "Finished"; });
        e.StartStop();
        Assert.Equal(TimerState.Running, e.State);
        await WaitFor(() => files.Last == "Starting in 00:00:03");
        Assert.Equal(@"C:\out\countdown.txt", files.LastPath);

        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "Starting in 00:00:02");

        var completed = false;
        e.Completed += (_, _) => completed = true;
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitFor(() => completed);
        Assert.Equal("Finished", files.Last);
        Assert.Equal("Finished", e.CountdownOutput);
        Assert.Equal(TimerState.Idle, e.State);
        await WaitFor(() => platform.Beeps == 1);
        Assert.False(platform.HasRunningTimers);
    }

    [Fact]
    public async Task Writes_only_when_text_changes()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countdown, s => { s.Minutes = 1; });
        e.StartStop();
        await WaitFor(() => files.Writes.Count == 1);
        await Task.Delay(400); // several ticks, same second
        Assert.Single(files.Writes);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Writes.Count == 2);
        e.StartStop();
    }

    [Fact]
    public async Task Pause_and_resume_keep_remaining_time()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countdown, s => { s.Minutes = 1; });
        e.StartStop();
        await WaitFor(() => files.Last == "Starting in 00:01:00");
        clock.Advance(TimeSpan.FromSeconds(20));
        await WaitFor(() => files.Last == "Starting in 00:00:40");

        e.PauseResume();
        Assert.Equal(TimerState.Paused, e.State);
        Assert.True(e.CanPauseResume);
        clock.Advance(TimeSpan.FromMinutes(5)); // time passes while paused

        e.PauseResume();
        Assert.Equal(TimerState.Running, e.State);
        // same text as before the pause => no redundant write (legacy behaviour); advance to prove remaining time was kept
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "Starting in 00:00:39");
        e.StartStop();
    }

    [Fact]
    public async Task AddMinute_extends_countdown_and_Reset_restarts()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countdown, s => { s.Minutes = 1; });
        e.StartStop();
        await WaitFor(() => files.Last == "Starting in 00:01:00");
        e.AddMinutes(1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "Starting in 00:01:59");
        e.AddMinutes(-1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "Starting in 00:00:58");

        e.Reset();
        await WaitFor(() => files.Last == "Starting in 00:01:00" && e.State == TimerState.Running);
        e.StartStop();
        Assert.Equal(TimerState.Idle, e.State);
        Assert.Equal("", files.Last); // forced stop clears the file like legacy
    }

    [Fact]
    public async Task CountUp_counts_and_add_minute_jumps()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countup);
        e.StartStop();
        await WaitFor(() => files.Last == "00:00:00");
        clock.Advance(TimeSpan.FromSeconds(61));
        await WaitFor(() => files.Last == "00:01:01");
        e.AddMinutes(1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "00:02:02");
        e.StartStop();
    }

    [Fact]
    public async Task Url_Start_while_running_restarts_with_new_duration()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countdown, s => { s.Minutes = 10; });
        e.StartStop();
        await WaitFor(() => files.Last == "Starting in 00:10:00");
        e.Apply(new UrlCommand(CommandAction.Start, 2, "countdown"));
        await WaitFor(() => files.Last == "Starting in 00:02:00");
        e.Apply(new UrlCommand(CommandAction.Add, 1, "countdown"));
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "Starting in 00:02:59");
        e.Apply(new UrlCommand(CommandAction.Stop, -1, "countdown"));
        Assert.Equal(TimerState.Idle, e.State);
    }

    [Fact]
    public async Task Custom_multi_token_template_advances_each_second()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countup, s => { s.Output = "{0:mm}:{0:ss}"; });
        e.StartStop();
        await WaitFor(() => files.Last == "00:00");
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => files.Last == "00:01");
        e.StartStop();
    }

    [Fact]
    public void Invalid_template_blocks_start_with_legacy_message()
    {
        var (e, _, _, _, _) = Create(TimerKind.Countdown, s => { s.Output = "{0:zz}"; });
        e.StartStop();
        Assert.Equal(TimerState.Idle, e.State);
        Assert.Equal(OutputFormatter.InvalidFormatMessage, e.CountdownOutput);
    }

    [Fact]
    public async Task Time_timer_renders_clock_and_has_no_pause()
    {
        var prev = System.Globalization.CultureInfo.DefaultThreadCurrentCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var (e, clock, files, _, _) = Create(TimerKind.Time, s => { s.OutputStyle = 3; });
            clock.Now = new DateTime(2026, 1, 1, 21, 10, 5);
            e.StartStop();
            await WaitFor(() => files.Last == "21:10:05");
            Assert.False(e.CanPauseResume);
            e.StartStop();
        }
        finally { System.Globalization.CultureInfo.DefaultThreadCurrentCulture = prev; }
    }

    [Fact]
    public async Task Styles_are_gated_by_pro()
    {
        var store = new InMemorySettingsStore();
        var global = new GlobalSettings(store, @"C:\out");
        var settings = new TimerSettings(store, TimerKind.Countdown) { OutputStyle = 2, Minutes = 0, Seconds = 30 };
        var pro = new ProEntitlement(global) { ForceProInDebug = false };
        var files = new FakeFiles();
        var e = new TimerEngine(settings, global, pro, files, new FakePlatform(), new FakeClock());
        Assert.Equal(0, e.EffectiveOutputStyle);
        e.StartStop();
        await WaitFor(() => files.Last == "Starting in 00:00:30");
        e.StartStop();

        global.IsGold = true;
        Assert.Equal(2, e.EffectiveOutputStyle);
        e.StartStop();
        await WaitFor(() => files.Last == "30");
        e.StartStop();
    }

    [Fact]
    public async Task Write_failures_surface_error_after_retries()
    {
        var (e, clock, files, _, _) = Create(TimerKind.Countdown, s => { s.Minutes = 1; });
        files.FailWrites = true;
        e.StartStop();
        for (var i = 0; i < 6; i++) { clock.Advance(TimeSpan.FromSeconds(1)); await Task.Delay(150); }
        await WaitFor(() => e.CountdownOutput.Contains("Ensure app has access"));
        e.StartStop();
    }
}

