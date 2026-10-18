namespace MyStreamTimer.Core.Services;

/// <summary>Marshals callbacks while a UI is alive, including a shutdown check when queued work executes.</summary>
public sealed class ShutdownDispatcher(Func<bool> hasThreadAccess, Func<Action, bool> enqueue)
{
    int shuttingDown;

    public bool IsShuttingDown => Volatile.Read(ref shuttingDown) != 0;

    public void Shutdown() => Interlocked.Exchange(ref shuttingDown, 1);

    public bool TryEnqueue(Action action)
    {
        if (IsShuttingDown)
            return false;
        return enqueue(() =>
        {
            if (!IsShuttingDown)
                action();
        });
    }

    public void Dispatch(Action action)
    {
        if (IsShuttingDown)
            return;
        if (hasThreadAccess())
            action();
        else
            TryEnqueue(action);
    }
}
