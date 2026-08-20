using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.WinUI.Services;
using Windows.Storage;

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

    /// <summary>The native window handle (HWND) of the main window.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>Application-wide service container.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Shorthand for <c>Services.GetRequiredService&lt;T&gt;()</c>.</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public App()
    {
        InitializeComponent();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        UnhandledException += OnUnhandledException;
        Services = ConfigureServices();
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
        var activation = GetService<ActivationService>();
        var timers = GetService<TimerHost>();
        var settings = GetService<GlobalSettings>();
        var windowService = GetService<WindowService>();
        var store = GetService<StoreService>();

        // Capture the launch activation before any UI so a protocol launch boot-starts the right timer.
        activation.HandleLaunchActivation();
        activation.CommandReceived += (_, command) => timers.Dispatch(command);
        activation.Start();

        timers.AutoStart();
        if (activation.TakePendingCommand() is { } pending)
        {
            timers.Dispatch(pending);
        }

        Window = new MainWindow();
        windowService.Initialize(Window);
        Window.Activate();

        settings.TimesUsed++;
        if (settings.TimesUsed == 10)
        {
            _ = store.RequestRatingAsync();
        }

        _ = store.RefreshLicensesAsync();

        // Upgrading users (TimesUsed was already >= 1 before this launch's increment) see the welcome-back sheet once.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            async () => await Views.WelcomeBackDialog.TryShowAsync(settings));
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var logs = Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");
            Directory.CreateDirectory(logs);
            var file = Path.Combine(logs, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, $"{DateTime.Now:O}{Environment.NewLine}{e.Message}{Environment.NewLine}{e.Exception}");
            Debug.WriteLine($"[App] Unhandled exception logged to {file}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Failed to write crash log: {ex.Message}");
        }
    }
}
