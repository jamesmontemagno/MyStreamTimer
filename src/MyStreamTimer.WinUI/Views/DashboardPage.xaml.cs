using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>
/// Home dashboard: every timer as a live card with quick actions. The shell passes its <see cref="ShellViewModel"/>
/// as the navigation parameter; the page is cached so the <see cref="DashboardViewModel"/> is built once.
/// </summary>
public sealed partial class DashboardPage : Page
{
    private DashboardViewModel? _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    /// <summary>Never null after the first navigation.</summary>
    public DashboardViewModel ViewModel => _viewModel!;

    // ---------------- x:Bind helpers (DataTemplate functions must be static) ----------------

    /// <summary>UIA id per card control, e.g. <c>DashCountdownStartStop</c>.</summary>
    public static string AutomationId(TimerKind kind, string action) => $"Dash{kind}{action}";

    /// <summary>Accessible name such as "Start Countdown 1".</summary>
    public static string ActionName(string verb, string title) => $"{verb} {title}";

    public static string CardName(string title, string status) => $"{title}, {status}";

    /// <summary>Start is the accent call-to-action while idle; Stop is a regular button while running.</summary>
    public static Style StartStopStyle(bool isRunning) =>
        (Style)Application.Current.Resources[isRunning ? "TimerActionButtonStyle" : "TimerPrimaryActionButtonStyle"];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is null && e.Parameter is ShellViewModel shell)
        {
            _viewModel = DashboardViewModel.Create(shell);
            Bindings.Update();
        }
    }

    // ---------------- Card interactions ----------------

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        // Buttons and flyout items handle their own taps; only treat taps on inert card content as "open".
        if (IsInsideInteractiveControl(e.OriginalSource as DependencyObject, (DependencyObject)sender))
        {
            return;
        }

        if (ResolveTimer(sender) is { } timer)
        {
            OpenTimer(timer);
            e.Handled = true;
        }
    }

    private void OnOpenTimerClick(object sender, RoutedEventArgs e)
    {
        if (ResolveTimer(sender) is { } timer)
        {
            OpenTimer(timer);
        }
    }

    private void OnSeeProClick(object sender, RoutedEventArgs e) => NavigationService.Default.NavigateTo(NavigationService.ProTag);

    private static void OpenTimer(TimerViewModel timer) =>
        NavigationService.Default.NavigateTo(timer.IsLocked ? NavigationService.ProTag : timer.Kind.Id());

    /// <summary>ItemsRepeater does not set DataContext on x:Bind templates, so the item is carried in <c>Tag="{x:Bind}"</c>.</summary>
    private static TimerViewModel? ResolveTimer(object sender) =>
        sender is FrameworkElement element ? element.Tag as TimerViewModel ?? element.DataContext as TimerViewModel : null;

    private static bool IsInsideInteractiveControl(DependencyObject? source, DependencyObject card)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, card); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase or FlyoutPresenter)
            {
                return true;
            }
        }

        return false;
    }
}
