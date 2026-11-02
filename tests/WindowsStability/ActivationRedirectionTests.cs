// Managed completion/lifetime tests; the bounded wait is not a COM pump.
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MyStreamTimer.WinUI.Services;
using Xunit;

namespace MyStreamTimer.WindowsStability.Tests;

[Collection("Windows stability")]
public sealed class ActivationRedirectionTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(3);

    private static void Wait(WaitHandle handle)
    {
        if (!handle.WaitOne(Limit))
            throw new TimeoutException("The redirect did not signal completion.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Synchronous_and_asynchronous_failures_complete_and_propagate(bool asynchronous)
    {
        var failure = new InvalidOperationException("PRIVATE activation");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SafeWaitHandle? native = null;
        try
        {
            var error = Record.Exception(() => ActivationRedirection.Run(
                () => asynchronous ? pending.Task : throw failure,
                handle =>
                {
                    native = handle.SafeWaitHandle;
                    if (asynchronous)
                        pending.SetException(failure);
                    Wait(handle);
                }));
            Assert.Same(failure, error);
            Assert.True(SpinWait.SpinUntil(() => native!.IsClosed, Limit));
        }
        finally { native?.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Success_keeps_handle_open_through_wait_and_releases_it(bool alreadyCompleted)
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (alreadyCompleted)
            pending.SetResult();
        var caller = Environment.CurrentManagedThreadId;
        SafeWaitHandle? native = null;
        try
        {
            ActivationRedirection.Run(() =>
            {
                Assert.NotEqual(caller, Environment.CurrentManagedThreadId);
                Assert.Null(SynchronizationContext.Current);
                return pending.Task;
            }, handle =>
            {
                Assert.Equal(caller, Environment.CurrentManagedThreadId);
                native = handle.SafeWaitHandle;
                pending.TrySetResult();
                Wait(handle);
                Assert.False(native.IsClosed);
            });
            Assert.True(SpinWait.SpinUntil(() => native!.IsClosed, Limit));
        }
        finally { native?.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Canceled_operation_completes_and_propagates(bool alreadyCanceled)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (alreadyCanceled)
            pending.SetCanceled(cancellation.Token);
        SafeWaitHandle? native = null;
        try
        {
            var error = Record.Exception(() => ActivationRedirection.Run(() => pending.Task, handle =>
            {
                native = handle.SafeWaitHandle;
                pending.TrySetCanceled(cancellation.Token);
                Wait(handle);
            }));
            var canceled = Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
            Assert.True(SpinWait.SpinUntil(() => native!.IsClosed, Limit));
        }
        finally { native?.Dispose(); }
    }

    [Theory]
    [InlineData(TaskStatus.RanToCompletion)]
    [InlineData(TaskStatus.Faulted)]
    [InlineData(TaskStatus.Canceled)]
    public void Failed_native_wait_defers_cleanup_until_redirect_finishes(TaskStatus outcome)
    {
        AppDiagnostics.Records.Clear();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitFailure = new COMException("Wait failed", unchecked((int)0x80070057));
        var redirectFailure = new InvalidOperationException("PRIVATE activation");
        SafeWaitHandle? native = null;
        try
        {
            var error = Record.Exception(() => ActivationRedirection.Run(() => pending.Task, handle =>
            {
                native = handle.SafeWaitHandle;
                throw waitFailure;
            }));
            Assert.Same(waitFailure, error);
            Assert.False(native!.IsClosed);
            if (outcome == TaskStatus.Faulted)
                pending.SetException(redirectFailure);
            else if (outcome == TaskStatus.Canceled)
                pending.SetCanceled();
            else
                pending.SetResult();
            Assert.True(SpinWait.SpinUntil(() => native.IsClosed, Limit));
            if (outcome == TaskStatus.Faulted)
            {
                var record = Assert.Single(AppDiagnostics.Records);
                Assert.Equal("activation-redirect-failed", record.Event);
                Assert.Null(record.Details);
                Assert.Same(redirectFailure, Assert.IsType<AggregateException>(record.Exception).InnerException);
            }
            else
                Assert.Empty(AppDiagnostics.Records);
        }
        finally
        {
            pending.TrySetResult();
            native?.Dispose();
        }
    }

    [Fact]
    public void Native_wait_failure_after_completion_still_releases_handle()
    {
        SafeWaitHandle? native = null;
        var failure = new COMException("Wait failed");
        try
        {
            Assert.Same(failure, Record.Exception(() => ActivationRedirection.Run(() => Task.CompletedTask, handle =>
            {
                native = handle.SafeWaitHandle;
                Wait(handle);
                throw failure;
            })));
            Assert.True(SpinWait.SpinUntil(() => native!.IsClosed, Limit));
        }
        finally { native?.Dispose(); }
    }
}
