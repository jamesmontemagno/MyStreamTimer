using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MyStreamTimer.Core.Settings;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Main-window sizing/placement persistence, always-on-top, backdrop and theme for the main window and any
/// registered secondary (pop-out) windows. Also exposes the package version.
/// </summary>
public sealed class WindowService
{
    // 2 dashboard columns (2×300 + 12 gap) + page padding 72 + open nav pane 248 = 932 DIP.
    public const int DefaultWidth = 940;
    public const int DefaultHeight = 620;
    public const int MinWidth = 640;
    public const int MinHeight = 480;

    private readonly GlobalSettings _settings;
    private readonly List<Window> _windows = [];
    private Window? _mainWindow;
    private bool _isAlwaysOnTop;
    private string _theme = "system";

    public WindowService(GlobalSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Package version, e.g. "3.0.0.0".</summary>
    public static string Version
    {
        get
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
    }

    /// <summary>Current theme key: "system", "light" or "dark".</summary>
    public string Theme => _theme;

    public bool IsAlwaysOnTop => _isAlwaysOnTop;

    /// <summary>Configures the main window: size/min size, restored bounds, backdrop, theme, always-on-top.</summary>
    public void Initialize(Window window)
    {
        _mainWindow = window;
        _windows.Add(window);

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }

        if (!TryRestoreBounds(window))
        {
            ApplyDefaultBounds(window);
        }

        ApplyBackdrop(window);
        ApplyTheme(_settings.AppTheme);
        SetAlwaysOnTop(_settings.StayOnTop);

        window.AppWindow.Changed += OnMainWindowChanged;
        window.Closed += (_, _) => SaveBounds();
    }

    /// <summary>Registers a secondary window so theme and always-on-top are kept in sync with the main window.</summary>
    public void RegisterWindow(Window window)
    {
        if (_windows.Contains(window))
        {
            return;
        }

        _windows.Add(window);
        ApplyBackdrop(window);
        ApplyThemeTo(window, _theme);
        ApplyAlwaysOnTopTo(window, _isAlwaysOnTop);
        window.Closed += (_, _) => UnregisterWindow(window);
    }

    public void UnregisterWindow(Window window) => _windows.Remove(window);

    /// <summary>Applies <c>OverlappedPresenter.IsAlwaysOnTop</c> to every registered window.</summary>
    public void SetAlwaysOnTop(bool isAlwaysOnTop)
    {
        _isAlwaysOnTop = isAlwaysOnTop;
        foreach (var window in _windows)
        {
            ApplyAlwaysOnTopTo(window, isAlwaysOnTop);
        }
    }

    /// <summary>Applies "system" | "light" | "dark" to every registered window's root element and caption buttons.</summary>
    public void ApplyTheme(string theme)
    {
        _theme = string.IsNullOrWhiteSpace(theme) ? "system" : theme.ToLowerInvariant();
        foreach (var window in _windows)
        {
            ApplyThemeTo(window, _theme);
        }
    }

    /// <summary>Mica when supported, otherwise Desktop Acrylic; null when neither is available.</summary>
    public static void ApplyBackdrop(Window window)
    {
        if (window.SystemBackdrop is not null)
        {
            return;
        }

        if (MicaController.IsSupported())
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            window.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private static void ApplyAlwaysOnTopTo(Window window, bool isAlwaysOnTop)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isAlwaysOnTop;
        }
    }

    private static void ApplyThemeTo(Window window, string theme)
    {
        var requested = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = requested;
            var isDark = requested == ElementTheme.Dark
                || (requested == ElementTheme.Default && root.ActualTheme == ElementTheme.Dark);
            ApplyTitleBarColors(window, isDark);
        }
    }

    private static void ApplyTitleBarColors(Window window, bool isDark)
    {
        var titleBar = window.AppWindow.TitleBar;
        var foreground = isDark ? Colors.White : Colors.Black;
        var hover = isDark ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF) : Windows.UI.Color.FromArgb(0x10, 0, 0, 0);
        var pressed = isDark ? Windows.UI.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Windows.UI.Color.FromArgb(0x20, 0, 0, 0);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = isDark ? Colors.Gray : Colors.DarkGray;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
    }

    // ---------- bounds persistence ("x,y,w,h" in physical pixels) ----------

    /// <summary>Default size (DIP, DPI-scaled), capped to 85 % of the work area and centred on the display.</summary>
    private static void ApplyDefaultBounds(Window window)
    {
        var scale = GetScale(window);
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(DefaultWidth * scale), (int)(area.Width * 0.85));
        var height = Math.Min((int)(DefaultHeight * scale), (int)(area.Height * 0.85));
        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + (area.Height - height) / 2;
        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private bool TryRestoreBounds(Window window)
    {
        var parts = _settings.MainWindowBounds.Split(',');
        if (parts.Length != 4)
        {
            return false;
        }

        var values = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        var rect = new RectInt32(values[0], values[1], values[2], values[3]);
        if (rect.Width < 100 || rect.Height < 100)
        {
            return false;
        }

        // Only restore if the saved rectangle is (mostly) visible on a current display; clamp to its work area
        // so a size saved on a bigger monitor never overflows the current one.
        var area = DisplayArea.GetFromRect(rect, DisplayAreaFallback.None);
        if (area is null)
        {
            return false;
        }

        var work = area.WorkArea;
        var w = Math.Min(rect.Width, work.Width);
        var h = Math.Min(rect.Height, work.Height);
        var x = Math.Clamp(rect.X, work.X, Math.Max(work.X, work.X + work.Width - w));
        var y = Math.Clamp(rect.Y, work.Y, Math.Max(work.Y, work.Y + work.Height - h));
        window.AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        return true;
    }

    private void OnMainWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange)
        {
            SaveBounds();
        }
    }

    private void SaveBounds()
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            if (_mainWindow.AppWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
            {
                return; // don't persist minimized/maximized geometry
            }

            var p = _mainWindow.AppWindow.Position;
            var s = _mainWindow.AppWindow.Size;
            _settings.MainWindowBounds = string.Create(CultureInfo.InvariantCulture, $"{p.X},{p.Y},{s.Width},{s.Height}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowService] SaveBounds failed: {ex.Message}");
        }
    }

    private static double GetScale(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        return GetDpiForWindow(hwnd) / 96.0;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);
}

