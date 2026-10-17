using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Timers;

public class CommandBoundaryTests
{
    static TimerEngine Create(TimerKind kind, FakeFiles files, FakePlatform platform) =>
        new(new TimerSettings(new InMemorySettingsStore(), kind),
            new GlobalSettings(new InMemorySettingsStore(), @"C:\out"),
            new ProEntitlement(new GlobalSettings(new InMemorySettingsStore(), @"C:\out")),
            files, platform, new FakeClock());

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
