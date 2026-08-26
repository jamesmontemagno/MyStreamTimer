using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>Pro upsell and purchase management.</summary>
public sealed partial class ProPage : Page
{
    public ProPage()
    {
        ViewModel = App.GetService<ProViewModel>();
        InitializeComponent();
    }

    public ProViewModel ViewModel { get; }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static bool Not(bool value) => !value;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Activate();
        _ = ViewModel.RefreshFromStoreAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Deactivate();
        base.OnNavigatedFrom(e);
    }
}
