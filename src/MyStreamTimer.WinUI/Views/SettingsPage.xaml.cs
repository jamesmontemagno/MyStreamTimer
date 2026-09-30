using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>Settings: output folder, appearance, pop-out appearance (Pro) and data reset.</summary>
public sealed partial class SettingsPage : Page
{
    private Flyout? _openIconFlyout;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static bool Not(bool value) => !value;

    public static bool CanUseDefault(bool isDefault, bool isBusy) => !isDefault && !isBusy;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Activate();
        ViewModel.NavigateToProRequested += OnNavigateToProRequested;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.NavigateToProRequested -= OnNavigateToProRequested;
        ViewModel.Deactivate();
        base.OnNavigatedFrom(e);
    }

    private void OnNavigateToProRequested(object? sender, EventArgs e) => Frame?.Navigate(typeof(ProPage));

    // ---------------- Timer icon picker ----------------

    private void OnIconFlyoutOpened(object? sender, object e)
    {
        if (sender is not Flyout flyout)
        {
            return;
        }

        _openIconFlyout = flyout;
        if (flyout.Content is GridView grid && grid.Tag is TimerAppearanceItem item)
        {
            grid.SelectedItem = item.CurrentChoice;
        }
    }

    private void OnIconFlyoutClosed(object? sender, object e)
    {
        if (ReferenceEquals(_openIconFlyout, sender))
        {
            _openIconFlyout = null;
        }
    }

    private void OnIconItemClick(object sender, ItemClickEventArgs e)
    {
        if (sender is GridView { Tag: TimerAppearanceItem item } && e.ClickedItem is IconChoice choice)
        {
            item.Glyph = choice.Glyph;
        }

        _openIconFlyout?.Hide();
    }
}
