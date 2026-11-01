using MyStreamTimer.Core.Services;

namespace MyStreamTimer.WinUI.Services;

internal static class SettingsResetConfirmation
{
    internal static async Task RunAsync(ShutdownDispatcher dispatcher, Func<Task<bool>> confirm, Action reset)
    {
        if (!dispatcher.IsShuttingDown)
        {
            // Keep the post-confirmation check and synchronous reset on the caller's UI context.
            var confirmed = await confirm();
            if (!confirmed)
                return;

            if (!dispatcher.IsShuttingDown)
            {
                reset();
                return;
            }
        }

        AppDiagnostics.Record("settings-reset-skipped", "Settings reset was skipped because shutdown has started.");
    }
}
