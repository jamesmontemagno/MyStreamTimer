using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>The main application window.</summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>The UI thread dispatcher; use to marshal calls to the UI thread.</summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static ShutdownDispatcher UiDispatcher { get; private set; } = null!;

    private bool shutdownCompleted;

    /// <summary>The native window handle (HWND) of the main window.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>Application-wide service container.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Shorthand for <c>Services.GetRequiredService&lt;T&gt;()</c>.</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public App()
    {
        UnhandledException += OnUnhandledException;
        AppDiagnostics.Record("app-construction");
        InitializeComponent();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        UiDispatcher = new(() => DispatcherQueue.HasThreadAccess,
            action => DispatcherQueue.TryEnqueue(() => action()));
        Services = ConfigureServices();
        AppDiagnostics.Record("services-ready");
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var folders = new DefaultFolderProvider();
        folders.LogDiagnostics();

        services.AddSingleton(folders);
        services.AddSingleton<ISettingsStore, LocalSettingsStore>();
        services.AddSingleton(sp => new GlobalSettings(sp.GetRequiredService<ISettingsStore>(), folders.DefaultDirectoryPath));
        services.AddSingleton<ProEntitlement>();
        services.AddSingleton<IFileOutputService, FileOutputService>();
        services.AddSingleton<IClock>(SystemClock.Instance);

        services.AddSingleton<BeepService>();
        services.AddSingleton<PowerService>();
        services.AddSingleton<TimerPlatformService>();
        services.AddSingleton<ITimerPlatform>(sp => sp.GetRequiredService<TimerPlatformService>());
        services.AddSingleton<TimerHost>();

        services.AddSingleton<ActivationService>();
        services.AddSingleton<WindowService>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<LauncherService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<FolderService>();
        services.AddSingleton<StoreService>();
        services.AddSingleton<PopOutService>();

        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<ViewModels.AutomationViewModel>();
        services.AddTransient<ViewModels.ProViewModel>();
        services.AddTransient<ViewModels.AboutViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppDiagnostics.Record("launch-services");
        var activation = GetService<ActivationService>();
        var timers = GetService<TimerHost>();
        var settings = GetService<GlobalSettings>();
        var windowService = GetService<WindowService>();
        var store = GetService<StoreService>();

        // Capture the launch activation before any UI so a protocol launch boot-starts the right timer.
        activation.HandleLaunchActivation();
        activation.CommandReceived += (_, command) => timers.Dispatch(command);
        activation.Start();

        AppDiagnostics.Record("timer-autostart");
        timers.AutoStart();
        if (activation.TakePendingCommand() is { } pending)
        {
            timers.Dispatch(pending);
        }

        AppDiagnostics.Record("window-construction");
        Window = new MainWindow();
        Window.AppWindow.Closing += OnMainWindowClosing;
        Window.Closed += (_, _) =>
        {
            if (!UiDispatcher.IsShuttingDown)
            {
                UiDispatcher.Shutdown();
                AppDiagnostics.Record("shutdown-forced", "The window closed without a coordinated drain.");
                RunShutdownStep("activation", activation.Stop);
                _ = Task.Run(() => RunShutdownStep("timers", timers.Dispose));
                RunShutdownStep("power", () => GetService<PowerService>().Dispose());
            }
        };
        windowService.Initialize(Window);
        Window.Activate();
        AppDiagnostics.Record("window-activated");
        AppDiagnostics.RecordEnvironment();

        settings.TimesUsed++;
        if (settings.TimesUsed == 10)
        {
            _ = store.RequestRatingAsync();
        }

        _ = store.RefreshLicensesAsync();

        // Upgrading users (TimesUsed was already >= 1 before this launch's increment) see the welcome-back sheet once.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            async () =>
            {
                if (!UiDispatcher.IsShuttingDown)
                    await Views.WelcomeBackDialog.TryShowAsync(settings);
            });
    }

    private async void OnMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (shutdownCompleted)
            return;

        args.Cancel = true;
        if (UiDispatcher.IsShuttingDown)
            return;

        UiDispatcher.Shutdown();
        AppDiagnostics.Record("shutdown-start");
        try
        {
            RunShutdownStep("activation", () => GetService<ActivationService>().Stop());
            // File-system calls may be blocked on a network share; never hold the UI behind an engine lock.
            var shutdown = Task.Run(async () => await GetService<TimerHost>().DisposeAsync());
            RunShutdownStep("power", () => GetService<PowerService>().Dispose());
            RunShutdownStep("pop-outs", () => GetService<PopOutService>().CloseAll());
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            AppDiagnostics.Record("shutdown-drained");
        }
        catch (TimeoutException ex)
        {
            AppDiagnostics.Record("shutdown-timeout", "Pending timer I/O did not drain before closing.", ex);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("shutdown-failed", exception: ex);
        }
        finally
        {
            shutdownCompleted = true;
            try
            {
                Window.Close();
            }
            catch (Exception ex)
            {
                AppDiagnostics.Record("shutdown-close-failed", exception: ex);
                // The UI is already shut down; do not leave a live, unusable window or rethrow from async void.
                Environment.Exit(1);
            }
        }
    }

    private static void RunShutdownStep(string step, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("shutdown-step-failed", step, ex);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppDiagnostics.Record("xaml-unhandled", exception: e.Exception);
    }
}
