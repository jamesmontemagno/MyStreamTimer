using Microsoft.UI.Xaml;

namespace MyStreamTimer.WinUI;

/// <summary>
/// The application window: Mica backdrop, extended title bar and the <see cref="Views.ShellPage"/> that hosts
/// the sidebar and content frame. Sizing, placement and theme are applied by <c>WindowService.Initialize</c>.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
    }
}

