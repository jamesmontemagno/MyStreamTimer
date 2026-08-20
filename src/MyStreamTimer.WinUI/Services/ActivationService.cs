using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;
using MyStreamTimer.Core.Automation;
using Windows.ApplicationModel.Activation;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Receives launch-time and redirected (single-instance) activations, parses <c>mystreamtimer://</c> URIs
/// with <see cref="UrlCommandParser"/> and raises <see cref="CommandReceived"/> on the UI thread.
/// </summary>
public sealed class ActivationService
{
    /// <summary>Raised on the UI thread for every valid or invalid parsed URL command.</summary>
    public event EventHandler<UrlCommand>? CommandReceived;

    /// <summary>Command that arrived with the initial launch, consumed by the shell before the UI shows.</summary>
    public UrlCommand? PendingCommand { get; private set; }

    /// <summary>Subscribes to redirected activations. Call once, on the UI thread, after the app object exists.</summary>
    public void Start()
    {
        AppInstance.GetCurrent().Activated += OnActivated;
    }

    /// <summary>Handles the activation that launched this process and stores any command as <see cref="PendingCommand"/>.</summary>
    public void HandleLaunchActivation()
    {
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (TryParse(args, out var command))
        {
            PendingCommand = command;
        }
    }

    /// <summary>Clears <see cref="PendingCommand"/> after the shell has dispatched it.</summary>
    public UrlCommand? TakePendingCommand()
    {
        var pending = PendingCommand;
        PendingCommand = null;
        return pending;
    }

    /// <summary>Dispatches an activation (launch-time or redirected) to the UI thread.</summary>
    public void HandleActivation(AppActivationArguments args)
    {
        var hasCommand = TryParse(args, out var command);

        App.DispatcherQueue.TryEnqueue(() =>
        {
            BringToFront();
            if (hasCommand)
            {
                CommandReceived?.Invoke(this, command);
            }
        });
    }

    private void OnActivated(object? sender, AppActivationArguments args) => HandleActivation(args);

    private static bool TryParse(AppActivationArguments args, out UrlCommand command)
    {
        command = UrlCommand.None;
        if (args.Kind != ExtendedActivationKind.Protocol || args.Data is not IProtocolActivatedEventArgs protocol)
        {
            return false;
        }

        var uri = protocol.Uri.AbsoluteUri;
        Debug.WriteLine($"[ActivationService] Protocol activation: {uri}");
        command = UrlCommandParser.Parse(uri);
        return true;
    }

    /// <summary>Activates the main window and forces it to the foreground.</summary>
    public static void BringToFront()
    {
        var window = App.Window;
        if (window is null)
        {
            return;
        }

        window.AppWindow.Show();
        window.Activate();
        SetForegroundWindow(App.WindowHandle);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
