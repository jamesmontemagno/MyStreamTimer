namespace MyStreamTimer.WinUI.Services;

internal static class ActivationRedirection
{
    internal static void Run(Func<Task> redirect, Action<WaitHandle> wait)
    {
        var task = Task.Run(redirect);
        var waitCompleted = false;
        try
        {
            // Task completion signals for success, faults and cancellation. The caller must pump COM.
            wait(((IAsyncResult)task).AsyncWaitHandle);
            waitCompleted = true;
            task.GetAwaiter().GetResult();
        }
        finally
        {
            // A failed native wait can return before redirection finishes. Keep its handle alive
            // until both sides are done, and observe any later fault rather than abandoning it.
            _ = task.ContinueWith(completed =>
            {
                try
                {
                    if (!waitCompleted && completed.Exception is { } error)
                        AppDiagnostics.Record("activation-redirect-failed", exception: error);
                }
                finally
                {
                    completed.Dispose();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
