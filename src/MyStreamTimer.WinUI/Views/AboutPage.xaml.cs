using Microsoft.UI.Xaml.Controls;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>About: version, open-source blurb, links and diagnostics.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        ViewModel = App.GetService<AboutViewModel>();
        InitializeComponent();
    }

    public AboutViewModel ViewModel { get; }
}
