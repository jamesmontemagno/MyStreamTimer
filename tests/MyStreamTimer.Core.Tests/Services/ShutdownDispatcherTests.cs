using MyStreamTimer.Core.Services;

namespace MyStreamTimer.Core.Tests.Services;

public class ShutdownDispatcherTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispatch_runs_on_the_ui_thread_or_enqueues(bool hasAccess)
    {
        Action? queued = null;
        var dispatcher = new ShutdownDispatcher(() => hasAccess, action => { queued = action; return true; });
        var called = false;

        dispatcher.Dispatch(() => called = true);

        Assert.Equal(hasAccess, called);
        if (!hasAccess)
            Assert.IsType<Action>(queued)();
        Assert.True(called);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Shutdown_rejects_new_callbacks_and_drops_already_queued_callbacks(bool hasAccess)
    {
        var queue = new Queue<Action>();
        var dispatcher = new ShutdownDispatcher(() => hasAccess, action => { queue.Enqueue(action); return true; });
        var calls = 0;
        Assert.True(dispatcher.TryEnqueue(() => calls++));

        dispatcher.Shutdown();
        dispatcher.Shutdown();
        dispatcher.Dispatch(() => calls++);
        Assert.False(dispatcher.TryEnqueue(() => calls++));
        Assert.Single(queue)();

        Assert.True(dispatcher.IsShuttingDown);
        Assert.Equal(0, calls);
        Assert.Single(queue);
    }

    [Fact]
    public void Queue_rejection_is_not_reported_as_success()
    {
        var dispatcher = new ShutdownDispatcher(() => false, _ => false);
        Assert.False(dispatcher.TryEnqueue(() => Assert.Fail("Rejected callback ran.")));
    }

    [Fact]
    public void Callback_exceptions_are_not_swallowed()
    {
        var dispatcher = new ShutdownDispatcher(() => true, _ => false);
        Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(() => throw new InvalidOperationException()));
    }
}
