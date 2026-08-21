using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;
using MyStreamTimer.WinUI.ViewModels;
using Windows.System;

namespace MyStreamTimer.WinUI.Views;

/// <summary>
/// App shell: grouped <see cref="NavigationView"/> (timers, automation, Pro, about, settings) and the content
/// <see cref="Frame"/>. Selection is persisted to <c>GlobalSettings.LastSelectedPage</c>, URL commands and
/// <see cref="NavigationService"/> requests select the matching item, and Ctrl+Shift+1…7 / Ctrl+Shift+H / Ctrl+, are handled here.
/// </summary>
public sealed partial class ShellPage : Page
{
    private const int OemCommaVirtualKey = 188;

    private static readonly string[] TimerTagsByAcceleratorOrder =
    [
        "countdown", "countdown2", "countdown3", "countdown4", "countup", "countup2", "time",
    ];

    public ShellViewModel ViewModel { get; } = ShellViewModel.Create();

    public ShellPage()
    {
        InitializeComponent();

        // Ctrl+, (OEM comma) has no named VirtualKey member, so it is added in code.
        var settingsAccelerator = new KeyboardAccelerator { Key = (VirtualKey)OemCommaVirtualKey, Modifiers = VirtualKeyModifiers.Control };
        settingsAccelerator.Invoked += OnSettingsAccelerator;
        KeyboardAccelerators.Add(settingsAccelerator);

        ViewModel.TimerCommandDispatched += (_, kind) => SelectByTag(kind.Id());
        NavigationService.Default.NavigationRequested += OnNavigationRequested;

        Loaded += (_, _) =>
        {
            if (Nav.SelectedItem is null)
            {
                SelectByTag(ViewModel.InitialPageTag);
            }
        };
    }

    private void OnNavigationRequested(object? sender, string tag)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            SelectByTag(tag);
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => SelectByTag(tag));
        }
    }

    /// <summary>Selects the sidebar item for <paramref name="tag"/> (which in turn navigates); falls back to Home.</summary>
    private void SelectByTag(string tag)
    {
        if (tag == NavigationService.SettingsTag)
        {
            Nav.SelectedItem = Nav.SettingsItem;
            return;
        }

        var item = FindItem(tag) ?? HomeItem;
        if (ReferenceEquals(Nav.SelectedItem, item))
        {
            return;
        }

        Nav.SelectedItem = item;
    }

    private NavigationViewItem? FindItem(string tag) =>
        Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag is string itemTag && string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase));

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(NavigationService.SettingsTag);
            return;
        }

        if (args.SelectedItemContainer?.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        ViewModel.LastSelectedPage = tag;
        var transition = new EntranceNavigationTransitionInfo();

        if (ViewModel.GetTimer(tag) is { } timer)
        {
            if (ContentFrame.Content is TimerPage current && ReferenceEquals(current.ViewModel, timer))
            {
                return;
            }

            ContentFrame.Navigate(typeof(TimerPage), timer, transition);
            return;
        }

        if (tag == NavigationService.HomeTag)
        {
            if (ContentFrame.CurrentSourcePageType != typeof(DashboardPage))
            {
                ContentFrame.Navigate(typeof(DashboardPage), ViewModel, transition);
            }

            return;
        }

        var pageType = tag switch
        {
            NavigationService.SettingsTag => typeof(SettingsPage),
            NavigationService.AutomationTag => typeof(AutomationPage),
            NavigationService.ProTag => typeof(ProPage),
            NavigationService.AboutTag => typeof(AboutPage),
            _ => typeof(TimerPage),
        };

        if (pageType == typeof(TimerPage))
        {
            SelectByTag(NavigationService.HomeTag);
            return;
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, null, transition);
        }
    }

    private void OnTimerAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var index = sender.Key - VirtualKey.Number1;
        if (index < 0 || index >= TimerTagsByAcceleratorOrder.Length)
        {
            return;
        }

        SelectByTag(TimerTagsByAcceleratorOrder[index]);
        args.Handled = true;
    }

    private void OnHomeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SelectByTag(NavigationService.HomeTag);
        args.Handled = true;
    }

    private void OnSettingsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SelectByTag(NavigationService.SettingsTag);
        args.Handled = true;
    }
}
