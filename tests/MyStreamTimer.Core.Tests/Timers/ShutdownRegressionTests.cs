using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class ShutdownRegressionTests
{
    static TimerEngine Create(TimerKind kind, FakeFiles files, FakePlatform platform, FakeClock? clock = null)
    {
        var store = new InMemorySettingsStore();
        var global = new GlobalSettings(store, @"C:\out") { IsGold = true };
        var settings = new TimerSettings(store, kind) { Minutes = 2, Seconds = 0 };
        return new(settings, global, new ProEntitlement(global), files, platform, clock ?? new());
    }

    [Theory]
    [InlineData(TimerKind.Countdown)]
    [InlineData(TimerKind.Countdown2)]
    [InlineData(TimerKind.Countdown3)]
    [InlineData(TimerKind.Countdown4)]
    [InlineData(TimerKind.Countup)]
    [InlineData(TimerKind.Countup2)]
    public async Task Protocol_stop_clears_paused_timers_discards_resume_and_allows_a_fresh_start(TimerKind kind)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        await using var engine = Create(kind, files, platform);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, text) => { if (text.Length > 0) rendered.TrySetResult(); };
        Assert.True(engine.Apply(UrlCommandParser.Parse($"mystreamtimer://{kind.Id()}/?secs=120")).Applied);
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(engine.Apply(UrlCommandParser.Parse($"mystreamtimer://{kind.Id()}/?pause")).Applied);

        Assert.True(engine.Apply(UrlCommandParser.Parse($"mystreamtimer://{kind.Id()}/?stop")).Applied);

        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(engine.CanPauseResume);
        Assert.False(platform.HasRunningTimers);
        Assert.Empty(files.Last);
        Assert.Empty(engine.CountdownOutput);
        Assert.Equal(CommandStatus.Ignored, engine.Apply(new(CommandAction.Resume, -1, kind.Id())).Status);
        Assert.Equal(CommandStatus.Ignored, engine.Apply(new(CommandAction.Stop, -1, kind.Id())).Status);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.True(engine.Apply(new(CommandAction.Start, 1, kind.Id())).Applied);
        Assert.Equal(TimerState.Running, engine.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Paused_stop_reports_file_failures_and_can_retry_the_clear(bool failPrepare, bool direct)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        await using var engine = Create(TimerKind.Countdown, files, platform);
        engine.StartStop();
        Assert.True(engine.PauseResume().Applied);
        files.FailPrepare = failPrepare;
        files.FailWrites = !failPrepare;
        Exception? fault = null;
        engine.Faulted += (_, exception) => fault = exception;

        var result = direct ? engine.Stop() : engine.Apply(new(CommandAction.Stop, -1, "countdown"));

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Contains("could not be cleared", result.Message);
        Assert.NotNull(fault);
        Assert.Contains("could not be cleared", engine.CountdownOutput);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(engine.CanPauseResume);
        Assert.False(platform.HasRunningTimers);
        files.FailPrepare = files.FailWrites = false;
        Assert.True(engine.Stop().Applied);
        Assert.Empty(files.Last);
        Assert.Empty(engine.CountdownOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Async_dispose_drains_an_in_flight_write_and_rejects_late_commands(bool paused)
    {
        var files = new FakeFiles();
        var platform = new FakePlatform();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        files.OnWrite = _ =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)), "Blocked output was not released.");
        };
        var engine = Create(TimerKind.Countdown, files, platform);
        var events = 0;
        engine.TextChanged += (_, _) => Interlocked.Increment(ref events);
        try
        {
            engine.StartStop();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            if (paused)
                Assert.True(engine.PauseResume().Applied);

            var drain = engine.DisposeAsync().AsTask();

            Assert.False(drain.IsCompleted);
            Assert.Equal(TimerState.Idle, engine.State);
            Assert.False(platform.HasRunningTimers);
            foreach (var action in Enum.GetValues<CommandAction>().Where(action => action != CommandAction.None))
                Assert.Equal(CommandStatus.Rejected, engine.Apply(new(action, 1, "countdown")).Status);
            Assert.Equal(CommandStatus.Rejected, engine.StartAtBoot().Status);
            engine.StartStop();
            release.Set();
            await drain.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, Volatile.Read(ref events));
            Assert.Single(files.Writes);
            Assert.DoesNotContain(string.Empty, files.Writes);
            await engine.DisposeAsync();
            engine.Dispose();
        }
        finally
        {
            release.Set();
            await engine.DisposeAsync();
        }
    }

    [Fact]
    public async Task Failed_output_retries_without_waiting_for_the_displayed_minute_to_change()
    {
        var files = new FakeFiles { FailWrites = true };
        var platform = new FakePlatform();
        await using var engine = Create(TimerKind.Time, files, platform);
        engine.Settings.OutputStyle = 0;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Faulted += (_, _) => failed.TrySetResult();
        engine.TextChanged += (_, text) => { if (text.Length > 0) recovered.TrySetResult(); };
        engine.StartStop();
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        files.FailWrites = false;

        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(files.Last);
        Assert.Equal(files.Last, engine.CountdownOutput);
        Assert.True(engine.IsBusy);
    }

    [Fact]
    public async Task Countdown_completes_and_beeps_only_after_failed_finish_output_recovers()
    {
        var clock = new FakeClock();
        var files = new FakeFiles();
        var platform = new FakePlatform();
        await using var engine = Create(TimerKind.Countdown, files, platform, clock);
        engine.Settings.Minutes = 0;
        engine.Settings.Seconds = 1;
        engine.Settings.Finish = "Finished";
        engine.Settings.MakeSound = true;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.TextChanged += (_, _) => rendered.TrySetResult();
        engine.Faulted += (_, _) => fault.TrySetResult();
        engine.Completed += (_, _) => completed.TrySetResult();
        engine.StartStop();
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        files.FailWrites = true;
        clock.Advance(TimeSpan.FromSeconds(2));
        await fault.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(completed.Task.IsCompleted);
        Assert.True(engine.IsBusy);
        Assert.Equal(0, platform.Beeps);

        files.FailWrites = false;

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await engine.DisposeAsync();
        Assert.Equal("Finished", files.Last);
        Assert.Equal("Finished", engine.CountdownOutput);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.False(platform.HasRunningTimers);
        Assert.Equal(1, platform.Beeps);
    }
}
