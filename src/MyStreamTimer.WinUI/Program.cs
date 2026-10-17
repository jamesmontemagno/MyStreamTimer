using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MyStreamTimer.WinUI.Services;

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
        AppDiagnostics.Initialize();
        try
        {
            return Run();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("entry-point-failure", exception: ex);
            _ = MessageBox(nint.Zero,
                "My Stream Timer couldn't start or continue and needs to close. Try reopening the app. " +
                "If the problem continues, collect the local diagnostics using the support guide.",
                "My Stream Timer", 0x10);
            return 1;
        }
    }

    private static int Run()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        AppDiagnostics.Record("instancing");

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
        AppDiagnostics.Record("activation", activatedArgs.Kind.ToString());
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEvent(nint lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(nint hEvent);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, nint[] pHandles, out uint dwIndex);
}
