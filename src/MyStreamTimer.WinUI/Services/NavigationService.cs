namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Lightweight in-process navigation requests for the shell. Pages and dialogs that need to jump to another
/// section (e.g. "See Pro options") call <see cref="NavigateTo"/> with a page tag; <c>ShellPage</c> listens and
/// selects the matching <c>NavigationViewItem</c> so the sidebar stays in sync.
/// </summary>
public sealed class NavigationService
{
    public const string CountdownTag = "countdown";
    public const string AutomationTag = "automation";
    public const string ProTag = "pro";
    public const string AboutTag = "about";
    public const string SettingsTag = "settings";

    /// <summary>Process-wide instance (the shell is a singleton, so no DI is required).</summary>
    public static NavigationService Default { get; } = new();

    /// <summary>Raised on the caller's thread with the requested page tag (timer id, "automation", "pro", "about", "settings").</summary>
    public event EventHandler<string>? NavigationRequested;

    public void NavigateTo(string tag) => NavigationRequested?.Invoke(this, tag);
}
