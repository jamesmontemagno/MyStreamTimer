using System.Diagnostics;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Views;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Creates and tracks one <see cref="PopOutWindow"/> per <see cref="TimerKind"/> (Pro feature). Windows are
/// reused when already open, follow always-on-top/theme through <see cref="WindowService"/>, and are closed
/// with the main window.
/// </summary>
public sealed class PopOutService
{
    private readonly Dictionary<TimerKind, PopOutWindow> _windows = [];
    private readonly ProEntitlement _pro;
    private readonly WindowService _windowService;
    private readonly GlobalSettings _settings;
    private readonly TimerHost _timers;
    private readonly ClipboardService _clipboard;
    private readonly LauncherService _launcher;
    private bool _isMainWindowHooked;

    public PopOutService(ProEntitlement pro, WindowService windowService, GlobalSettings settings, TimerHost timers,
        ClipboardService clipboard, LauncherService launcher)
    {
        _pro = pro;
        _windowService = windowService;
        _settings = settings;
        _timers = timers;
        _clipboard = clipboard;
        _launcher = launcher;
        _pro.Changed += OnProChanged;
    }

    /// <summary>Raised on the UI thread when pop-out font/colour settings change so open windows re-style live.</summary>
    public event EventHandler? AppearanceChanged;

    /// <summary>Raised when a pop-out opens or closes (argument = kind).</summary>
    public event EventHandler<TimerKind>? OpenStateChanged;

    /// <summary>Raised on the UI thread when a timer's display name or icon changes (argument = kind).</summary>
    public event EventHandler<TimerKind>? TimerAppearanceChanged;

    public bool IsOpen(TimerKind kind) => _windows.ContainsKey(kind);

    public IReadOnlyCollection<TimerKind> OpenKinds => _windows.Keys;

    public void NotifyAppearanceChanged() => AppearanceChanged?.Invoke(this, EventArgs.Empty);

    public void NotifyTimerAppearanceChanged(TimerKind kind) => TimerAppearanceChanged?.Invoke(this, kind);

    /// <summary>Shows (or activates) the pop-out for <paramref name="kind"/>. Returns false when not Pro.</summary>
    public bool Show(TimerKind kind)
    {
        if (!_pro.IsPro)
        {
            Debug.WriteLine($"[PopOutService] Pop-out for {kind.Id()} requires Pro");
            return false;
        }

        HookMainWindow();

        if (_windows.TryGetValue(kind, out var existing))
        {
            existing.Activate();
            return true;
        }

        var window = new PopOutWindow(kind, this, _timers.Engine(kind), _settings, _windowService, _clipboard, _launcher, _windows.Count);
        _windows[kind] = window;
        window.Closed += (_, _) =>
        {
            _windows.Remove(kind);
            OpenStateChanged?.Invoke(this, kind);
        };
        window.Activate();
        OpenStateChanged?.Invoke(this, kind);
        return true;
    }

    public void Close(TimerKind kind)
    {
        if (_windows.TryGetValue(kind, out var window))
        {
            window.Close();
        }
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToList())
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PopOutService] Close failed: {ex.Message}");
            }
        }

        _windows.Clear();
    }

    private void HookMainWindow()
    {
        if (_isMainWindowHooked || App.Window is null)
        {
            return;
        }

        _isMainWindowHooked = true;
        App.Window.Closed += (_, _) => CloseAll();
    }

    private void OnProChanged(object? sender, EventArgs e)
    {
        if (!_pro.IsPro)
        {
            App.DispatcherQueue.TryEnqueue(CloseAll);
        }
    }
}
