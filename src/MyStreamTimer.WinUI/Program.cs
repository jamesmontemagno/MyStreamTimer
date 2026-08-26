using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace MyStreamTimer.WinUI;

/// <summary>
/// Custom entry point (XAML-generated Main is disabled) that enforces single instancing: any secondary
/// launch — typically a <c>mystreamtimer://</c> protocol activation — is redirected to the running instance.
/// </summary>
public static class Program
{
    private const string InstanceKey = "main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (!DecideRedirection())
        {
            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

        return 0;
    }

    /// <summary>Returns true when this process redirected its activation to the main instance and should exit.</summary>
    private static bool DecideRedirection()
    {
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);

        if (mainInstance.IsCurrent)
        {
            return false;
        }

        RedirectActivationTo(activatedArgs, mainInstance);
        return true;
    }

    // Documented pattern (Microsoft Learn "App instancing with the app lifecycle API"): wait on a Win32
    // event while pumping COM messages, because RedirectActivationToAsync cannot be awaited on an STA thread.
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        var redirectEventHandle = CreateEvent(nint.Zero, true, false, null);
        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            SetEvent(redirectEventHandle);
        });

        _ = CoWaitForMultipleObjects(CWMO_DEFAULT, INFINITE, 1, [redirectEventHandle], out _);
    }

    private const uint CWMO_DEFAULT = 0;
    private const uint INFINITE = 0xFFFFFFFF;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEvent(nint lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(nint hEvent);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, nint[] pHandles, out uint dwIndex);
}
