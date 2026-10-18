using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class CommandBoundaryTests
{
    static TimerEngine Create(TimerKind kind, FakeFiles files, FakePlatform platform, IClock? clock = null) =>
        new(new TimerSettings(new InMemorySettingsStore(), kind),
            new GlobalSettings(new InMemorySettingsStore(), @"C:\out"),
            new ProEntitlement(new GlobalSettings(new InMemorySettingsStore(), @"C:\out")),
            files, platform, clock ?? new FakeClock());

    [Fact]
    public async Task Completion_during_stop_preparation_does_not_start_an_uninitialized_countdown()
    {
        var clock = new FakeClock();
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform, clock);
        engine.Settings.Minutes = 0;
        engine.Settings.Seconds = 1;
        using var completed = new ManualResetEventSlim();
        var runningTransitions = 0;
        engine.StateChanged += (_, _) =>
        {
            if (engine.IsBusy)
                Interlocked.Increment(ref runningTransitions);
        };
        engine.Completed += (_, _) => completed.Set();
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, text) =>
        {
            if (text == "Starting in 00:00:01")
                rendered.TrySetResult();
        };
        engine.StartStop();
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        files.OnPrepareTarget = () =>
        {
            files.OnPrepareTarget = null;
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "The old countdown did not complete during target preparation.");
        };

        engine.StartStop();

        Assert.True(completed.IsSet);
        Assert.Equal(1, Volatile.Read(ref runningTransitions));
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(platform.HasRunningTimers);
        Assert.Empty(files.Last);
    }

    [Theory]
    [InlineData(CommandAction.Pause, false, false)]
    [InlineData(CommandAction.Pause, true, false)]
    [InlineData(CommandAction.Resume, false, false)]
    [InlineData(CommandAction.Resume, true, false)]
    [InlineData(CommandAction.Reset, false, false)]
    [InlineData(CommandAction.Reset, true, false)]
    [InlineData(CommandAction.Pause, false, true)]
    [InlineData(CommandAction.Pause, true, true)]
    [InlineData(CommandAction.Resume, false, true)]
    [InlineData(CommandAction.Resume, true, true)]
    [InlineData(CommandAction.Reset, false, true)]
    [InlineData(CommandAction.Reset, true, true)]
    public void Failed_state_commands_report_rejection_and_preserve_state(CommandAction action, bool failPrepare, bool direct)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform);
        Assert.True(engine.Apply(new(CommandAction.Start, 5, "countdown")).Applied);
        if (action == CommandAction.Resume)
            Assert.True(engine.Apply(new(CommandAction.Pause, 0, "countdown")).Applied);
        var before = engine.State;
        files.FailPrepare = failPrepare;
        if (!failPrepare)
            engine.Settings.Output = "{0:zz}";

        var result = direct
            ? action == CommandAction.Reset ? engine.Reset() : engine.PauseResume()
            : engine.Apply(new(action, 0, "countdown"));

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(before, engine.State);
        Assert.True(engine.CanPauseResume);
        Assert.Equal(before == TimerState.Running, platform.HasRunningTimers);
    }

    [Theory]
    [InlineData(CommandAction.Pause, false)]
    [InlineData(CommandAction.Pause, true)]
    [InlineData(CommandAction.Resume, false)]
    [InlineData(CommandAction.Resume, true)]
    public void Clock_pause_and_resume_are_ignored(CommandAction action, bool running)
    {
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Time, new FakeFiles(), platform);
        if (running)
            engine.StartStop();
        var before = engine.State;

        var result = engine.Apply(new(action, 0, "time"));

        Assert.Equal(CommandStatus.Ignored, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(before, engine.State);
        Assert.False(engine.CanPauseResume);
        Assert.Equal(running, platform.HasRunningTimers);

        Assert.Equal(CommandStatus.Ignored, engine.PauseResume().Status);
        Assert.Equal(before, engine.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reset_reports_a_rejected_restart_instead_of_success(bool direct)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform);
        engine.StartStop();
        var preparations = 0;
        files.OnPrepareTarget = () => files.FailPrepare = ++preparations == 2;

        var result = direct ? engine.Reset() : engine.Apply(new(CommandAction.Reset, 0, "countdown"));

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(2, preparations);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(platform.HasRunningTimers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reset_does_not_restart_when_the_stop_cannot_clear_output(bool direct)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, _) => rendered.TrySetResult();
        engine.StartStop();
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        files.FailWrites = true;
        var preparations = 0;
        files.OnPrepareTarget = () => preparations++;

        var result = direct ? engine.Reset() : engine.Apply(new(CommandAction.Reset, 0, "countdown"));

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Contains("could not be cleared", result.Message);
        Assert.Equal(1, preparations);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(platform.HasRunningTimers);
        Assert.False(engine.CanPauseResume);
    }

    [Fact]
    public void Start_preserves_the_active_run_when_its_stop_is_rejected()
    {
        var files = new FakeFiles();
        using var engine = Create(TimerKind.Countdown, files, new FakePlatform());
        engine.StartStop();
        var preparations = 0;
        files.OnPrepareTarget = () =>
        {
            preparations++;
            files.FailPrepare = preparations == 1;
        };

        var result = engine.Apply(new(CommandAction.Start, 10, "countdown"));

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(1, preparations);
        Assert.Equal(TimerState.Running, engine.State);
    }

    [Fact]
    public async Task Rejected_reset_preserves_a_resumed_count_up_offset()
    {
        var clock = new FakeClock();
        var files = new FakeFiles();
        await using var engine = Create(TimerKind.Countup, files, new FakePlatform(), clock);
        Assert.True(engine.StartAtBoot(2.5f).Applied);
        Assert.True(engine.PauseResume().Applied);
        Assert.True(engine.PauseResume().Applied);
        var output = engine.Settings.Output;
        engine.Settings.Output = "{0:zz}";

        Assert.Equal(CommandStatus.Rejected, engine.Reset().Status);

        engine.Settings.Output = output;
        Assert.True(engine.PauseResume().Applied);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, text) =>
        {
            if (text.EndsWith("00:02:31", StringComparison.Ordinal))
                rendered.TrySetResult();
        };
        Assert.True(engine.PauseResume().Applied);
        // An unchanged resumed value need not raise TextChanged; verify the next second instead.
        clock.Advance(TimeSpan.FromSeconds(1));
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Invalid_configured_duration_rejects_direct_reset_before_stopping()
    {
        var clock = new FakeClock { Now = DateTime.MaxValue.AddMinutes(-10) };
        using var engine = Create(TimerKind.Countdown, new FakeFiles(), new FakePlatform(), clock);
        Assert.True(engine.StartAtBoot(5).Applied);
        engine.Settings.Minutes = 20;

        Assert.Equal(CommandStatus.Rejected, engine.Reset().Status);
        Assert.Equal(TimerState.Running, engine.State);
    }

    [Theory]
    [InlineData(TimerKind.Countdown)]
    [InlineData(TimerKind.Countdown2)]
    [InlineData(TimerKind.Countdown3)]
    [InlineData(TimerKind.Countdown4)]
    public void Cold_subtract_is_ignored_without_side_effects(TimerKind kind)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(kind, files, platform);
        foreach (var query in new[] { "subtractmins=1", "subtractsecs=30" })
        {
            var result = engine.Apply(UrlCommandParser.Parse($"mystreamtimer://{kind.Id()}/?{query}"));
            Assert.Equal(CommandStatus.Ignored, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
            Assert.Equal(TimerState.Idle, engine.State);
            Assert.Empty(engine.CountdownOutput);
            Assert.Empty(files.Writes);
            Assert.False(platform.HasRunningTimers);
        }
    }

    [Theory]
    [InlineData(CommandAction.Start, float.MaxValue)]
    [InlineData(CommandAction.Start, float.PositiveInfinity)]
    [InlineData(CommandAction.Start, float.NaN)]
    [InlineData(CommandAction.Add, float.MaxValue)]
    [InlineData(CommandAction.Subtract, float.MaxValue)]
    public async Task Invalid_numeric_commands_do_not_mutate_a_running_timer(CommandAction action, float minutes)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, _) => rendered.TrySetResult();
        Assert.True(engine.Apply(new(CommandAction.Start, 5, "countdown")).Applied);
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var before = engine.CountdownOutput;
        var result = engine.Apply(new(action, minutes, "countdown"));
        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(TimerState.Running, engine.State);
        Assert.True(platform.HasRunningTimers);
        Assert.Equal(before, engine.CountdownOutput);
    }

    [Fact]
    public void Invalid_start_and_boot_leave_a_cold_timer_idle()
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var engine = Create(TimerKind.Countdown, files, platform);
        Assert.Equal(CommandStatus.Rejected, engine.Apply(new(CommandAction.Start, float.MaxValue, "countdown")).Status);
        Assert.Equal(CommandStatus.Rejected, engine.StartAtBoot(float.PositiveInfinity).Status);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(platform.HasRunningTimers);
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Valid_paused_and_stopped_adjustments_keep_legacy_behavior()
    {
        using var engine = Create(TimerKind.Countdown, new FakeFiles(), new FakePlatform());
        Assert.True(engine.Apply(new(CommandAction.Start, 5, "countdown")).Applied);
        engine.PauseResume();
        Assert.True(engine.Apply(new(CommandAction.Subtract, 1, "countdown")).Applied);
        Assert.Equal(TimerState.Paused, engine.State);
        engine.Stop();
        Assert.True(engine.Apply(new(CommandAction.Subtract, 1, "countdown")).Applied);
        Assert.Equal(TimerState.Idle, engine.State);
    }

    [Fact]
    public void Count_up_tick_overflow_is_rejected_without_starting()
    {
        using var engine = Create(TimerKind.Countup, new FakeFiles(), new FakePlatform());
        Assert.Equal(CommandStatus.Rejected, engine.Apply(new(CommandAction.Add, float.MaxValue, "countup")).Status);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.True(engine.Apply(new(CommandAction.Subtract, 1, "countup")).Applied);
    }
}
