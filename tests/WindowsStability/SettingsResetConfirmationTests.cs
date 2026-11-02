using System.Collections.Concurrent;
using MyStreamTimer.Core.Services;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WindowsStability.Tests;

// Exact managed production continuation; no native dialog or owner settings are used.
[Collection("Windows stability")]
public sealed class SettingsResetConfirmationTests
{
    public SettingsResetConfirmationTests() => AppDiagnostics.Records.Clear();

    private static ShutdownDispatcher Dispatcher() => new(() => true, _ => false);

    [Fact]
    public async Task Active_confirmation_runs_reset_once()
    {
        var calls = new List<string>();
        await SettingsResetConfirmation.RunAsync(Dispatcher(),
            () => { calls.Add("confirm"); return Task.FromResult(true); },
            () => calls.Add("reset"));

        Assert.Equal(["confirm", "reset"], calls);
        Assert.Empty(AppDiagnostics.Records);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Declining_or_dismissing_does_not_reset_even_if_shutdown_started(bool shutdown)
    {
        var dispatcher = Dispatcher();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SettingsResetConfirmation.RunAsync(dispatcher, () => pending.Task,
            () => Assert.Fail("A declined reset ran."));
        if (shutdown)
            dispatcher.Shutdown();
        pending.SetResult(false);

        await operation.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Empty(AppDiagnostics.Records);
    }

    [Fact]
    public async Task Already_shutting_down_does_not_open_confirmation()
    {
        var dispatcher = Dispatcher();
        dispatcher.Shutdown();
        var confirmations = 0;
        var resets = 0;

        await SettingsResetConfirmation.RunAsync(dispatcher,
            () => { confirmations++; return Task.FromResult(true); }, () => resets++);

        Assert.Equal(0, confirmations);
        Assert.Equal(0, resets);
        AssertShutdownDiagnostic();
    }

    [Fact]
    public async Task Shutdown_during_confirmation_preserves_fixture_settings_and_avoids_closed_resource()
    {
        var dispatcher = Dispatcher();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = new Dictionary<string, string> { ["private-setting"] = "private-value" };
        var closed = false;
        var resets = 0;
        var operation = SettingsResetConfirmation.RunAsync(dispatcher, () => pending.Task, () =>
        {
            resets++;
            settings.Remove("private-setting");
            if (closed)
                throw new ObjectDisposedException("Closed-window fixture");
        });
        Assert.False(operation.IsCompleted);

        dispatcher.Shutdown();
        closed = true;
        pending.SetResult(true);
        await operation.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(0, resets);
        Assert.Equal("private-value", settings["private-setting"]);
        AssertShutdownDiagnostic();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Confirmation_continuation_keeps_ui_context_and_rechecks_shutdown(bool shutdown)
    {
        var previous = SynchronizationContext.Current;
        using var context = new QueuedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var dispatcher = Dispatcher();
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var uiThread = Environment.CurrentManagedThreadId;
            var resets = 0;
            var operation = SettingsResetConfirmation.RunAsync(dispatcher, () => pending.Task, () =>
            {
                Assert.Same(context, SynchronizationContext.Current);
                Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                resets++;
            });
            pending.SetResult(true);
            if (shutdown)
                dispatcher.Shutdown();
            context.RunNext();
            Assert.True(operation.IsCompletedSuccessfully, operation.Exception?.ToString());

            Assert.Equal(shutdown ? 0 : 1, resets);
            if (shutdown)
                AssertShutdownDiagnostic();
            else
                Assert.Empty(AppDiagnostics.Records);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmation_faults_propagate_without_reset(bool synchronous)
    {
        var error = new InvalidOperationException("Confirmation fixture failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SettingsResetConfirmation.RunAsync(Dispatcher(),
                () => synchronous ? throw error : Task.FromException<bool>(error),
                () => Assert.Fail("Reset followed failed confirmation.")));

        Assert.Same(error, actual);
        Assert.Empty(AppDiagnostics.Records);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmation_task_cancellation_is_not_swallowed_during_shutdown(bool shutdown)
    {
        var dispatcher = Dispatcher();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SettingsResetConfirmation.RunAsync(dispatcher, () => pending.Task,
            () => Assert.Fail("Reset followed cancelled confirmation."));
        if (shutdown)
            dispatcher.Shutdown();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        pending.SetCanceled(cancellation.Token);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            operation.WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(operation.IsCanceled);
        Assert.Empty(AppDiagnostics.Records);
    }

    [Fact]
    public async Task Reset_faults_propagate_without_success_diagnostics()
    {
        var error = new InvalidOperationException("Reset fixture failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SettingsResetConfirmation.RunAsync(Dispatcher(), () => Task.FromResult(true), () => throw error));

        Assert.Same(error, actual);
        Assert.Empty(AppDiagnostics.Records);
    }

    private static void AssertShutdownDiagnostic()
    {
        var record = Assert.Single(AppDiagnostics.Records);
        Assert.Equal("settings-reset-skipped", record.Event);
        Assert.Equal("Settings reset was skipped because shutdown has started.", record.Details);
        Assert.Null(record.Exception);
    }

    private sealed class QueuedContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public void RunNext()
        {
            Assert.True(_queue.TryTake(out var work, TimeSpan.FromSeconds(3)), "Continuation was not queued.");
            work.Callback(work.State);
        }

        public void Dispose() => _queue.Dispose();
    }
}
