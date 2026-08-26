using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;
using MyStreamTimer.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

namespace MyStreamTimer.WinUI.Views;

/// <summary>
/// Borderless, title-bar-less always-on-top-capable preview of one timer (Pro). Drag anywhere to move,
/// double-click or ESC to close, right-click for the context menu. Bounds persist per timer.
/// </summary>
public sealed partial class PopOutWindow : Window
{
    private const int DefaultWidthDip = 400;
    private const int DefaultHeightDip = 160;
    private const int EdgeMarginDip = 16;
    private const int CascadeOffsetDip = 32;

    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;

    private readonly TimerSettings _timerSettings;
    private readonly WindowService _windowService;
    private readonly ClipboardService _clipboard;
    private readonly LauncherService _launcher;
    private readonly int _cascadeIndex;
    private DateTime _lastPressUtc = DateTime.MinValue;
    private Point _lastPressPosition;
    private bool _isClosed;
    private bool _isRestoringBounds;

    public PopOutWindow(TimerKind kind, PopOutService popOuts, TimerEngine engine, GlobalSettings settings,
        WindowService windowService, ClipboardService clipboard, LauncherService launcher, int cascadeIndex)
    {
        Kind = kind;
        _timerSettings = engine.Settings;
        _windowService = windowService;
        _clipboard = clipboard;
        _launcher = launcher;
        _cascadeIndex = cascadeIndex;
        ViewModel = new PopOutViewModel(engine, settings, popOuts);

        InitializeComponent();

        Title = ViewModel.Title;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PopOutViewModel.Title) && !_isClosed)
            {
                Title = ViewModel.Title;
            }
        };
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.IsShownInSwitchers = true;

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = windowService.IsAlwaysOnTop;
        AppWindow.SetPresenter(presenter);

        if (!TryRestoreBounds())
        {
            ApplyDefaultBounds();
        }

        // Theme, backdrop and always-on-top stay in sync with the main window; unregistered on Closed by the service.
        _windowService.RegisterWindow(this);

        AppWindow.Changed += OnAppWindowChanged;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    public TimerKind Kind { get; }

    public PopOutViewModel ViewModel { get; }

    // ---------- sizing / placement ----------

    private double Scale => GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;

    private void ApplyDefaultBounds()
    {
        var scale = Scale;
        var width = (int)(DefaultWidthDip * scale);
        var height = (int)(DefaultHeightDip * scale);
        var margin = (int)(EdgeMarginDip * scale);
        var cascade = (int)(CascadeOffsetDip * scale) * _cascadeIndex;

        var work = GetMainDisplayWorkArea();
        var x = work.X + work.Width - width - margin - cascade;
        var y = work.Y + margin + cascade;

        _isRestoringBounds = true;
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        _isRestoringBounds = false;
    }

    private static RectInt32 GetMainDisplayWorkArea()
    {
        try
        {
            if (App.Window is { } main)
            {
                return DisplayArea.GetFromWindowId(main.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopOutWindow] DisplayArea lookup failed: {ex.Message}");
        }

        return DisplayArea.Primary.WorkArea;
    }

    private bool TryRestoreBounds()
    {
        var parts = _timerSettings.PopOutBounds.Split(',');
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
        if (rect.Width < 60 || rect.Height < 40)
        {
            return false;
        }

        if (DisplayArea.GetFromRect(rect, DisplayAreaFallback.None) is null)
        {
            return false;
        }

        _isRestoringBounds = true;
        AppWindow.MoveAndResize(rect);
        _isRestoringBounds = false;
        return true;
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isRestoringBounds || _isClosed || !(args.DidPositionChange || args.DidSizeChange))
        {
            return;
        }

        try
        {
            var p = sender.Position;
            var s = sender.Size;
            _timerSettings.PopOutBounds = string.Create(CultureInfo.InvariantCulture, $"{p.X},{p.Y},{s.Width},{s.Height}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopOutWindow] Save bounds failed: {ex.Message}");
        }
    }

    // ---------- input ----------

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            Root.Focus(FocusState.Programmatic);
        }
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            CloseSafely();
        }
    }

    private void OnRootPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        HoverChrome.Opacity = 1;
        HoverChrome.IsHitTestVisible = true;
    }

    private void OnRootPointerExited(object sender, PointerRoutedEventArgs e)
    {
        // PointerExited also fires when moving onto the close button; it stays inside Root's bounds so re-check.
        var pos = e.GetCurrentPoint(Root).Position;
        if (pos.X >= 0 && pos.Y >= 0 && pos.X < Root.ActualWidth && pos.Y < Root.ActualHeight)
        {
            return;
        }

        HoverChrome.Opacity = 0;
        HoverChrome.IsHitTestVisible = false;
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Root);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // The close button handles its own click; don't start a drag from it.
        if (e.OriginalSource is DependencyObject source && IsInside(source, CloseButton))
        {
            return;
        }

        // Manual double-click detection: the native drag loop started below swallows the second XAML tap.
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastPressUtc).TotalMilliseconds;
        var distance = Math.Abs(point.Position.X - _lastPressPosition.X) + Math.Abs(point.Position.Y - _lastPressPosition.Y);
        _lastPressUtc = now;
        _lastPressPosition = point.Position;

        if (elapsed <= GetDoubleClickTime() && distance < 8)
        {
            e.Handled = true;
            CloseSafely();
            return;
        }

        // Drag-to-move without a title bar: hand the press to the non-client caption handler.
        e.Handled = true;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ReleaseCapture();
        SendMessage(hwnd, WmNcLButtonDown, HtCaption, 0);
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e) => _clipboard.SetText(ViewModel.FilePath);

    private static bool IsInside(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private async void OnOpenFolderClick(object sender, RoutedEventArgs e) => await _launcher.OpenFolderAsync(ViewModel.FolderPath);

    private void OnResetBoundsClick(object sender, RoutedEventArgs e)
    {
        _timerSettings.PopOutBounds = string.Empty;
        ApplyDefaultBounds();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseSafely();

    private void CloseSafely()
    {
        if (_isClosed)
        {
            return;
        }

        try
        {
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopOutWindow] Close failed: {ex.Message}");
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        AppWindow.Changed -= OnAppWindowChanged;
        Activated -= OnActivated;
        ViewModel.Dispose();
    }

    // ---------- interop ----------

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}
