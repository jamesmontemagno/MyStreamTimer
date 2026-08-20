using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>Automation: URL examples, command builder and Stream Deck / OBS tips.</summary>
public sealed partial class AutomationPage : Page
{
    public AutomationPage()
    {
        ViewModel = App.GetService<AutomationViewModel>();
        InitializeComponent();
    }

    public AutomationViewModel ViewModel { get; }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void OnGeneratedUrlGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.SelectAll();
        }
    }
}
