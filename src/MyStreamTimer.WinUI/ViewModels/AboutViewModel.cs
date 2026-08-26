using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyStreamTimer.WinUI.Services;
using Windows.Storage;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>An external link shown as a button on the About page.</summary>
public sealed record LinkItem(string Title, string Glyph, string Url, IAsyncRelayCommand<string?> OpenCommand)
{
    public string AutomationName => $"Open {Title}";
}

/// <summary>About page: version, open-source blurb, links and diagnostics.</summary>
public sealed partial class AboutViewModel : ObservableObject
{
    public const string RepositoryUrl = "https://github.com/jamesmontemagno/mystreamtimer";
    public const string GitHubUrl = "http://www.github.com/jamesmontemagno";
    public const string TwitterUrl = "http://www.twitter.com/jamesmontemagno";
    public const string YouTubeUrl = "http://www.youtube.com/jamesmontemagno";
    public const string BlogUrl = "http://www.montemagno.com";

    private readonly LauncherService _launcher;
    private readonly ClipboardService _clipboard;

    public AboutViewModel(LauncherService launcher, ClipboardService clipboard)
    {
        _launcher = launcher;
        _clipboard = clipboard;

        Links =
        [
            new("Source on GitHub", "\uE943", RepositoryUrl, OpenUrlCommand),
            new("GitHub", "\uE716", GitHubUrl, OpenUrlCommand),
            new("X / Twitter", "\uE8BD", TwitterUrl, OpenUrlCommand),
            new("YouTube", "\uE714", YouTubeUrl, OpenUrlCommand),
            new("Blog", "\uE736", BlogUrl, OpenUrlCommand),
            new("Privacy policy", "\uE72E", ProViewModel.PrivacyUrl, OpenUrlCommand),
        ];
    }

    public string AppName => "My Stream Timer";

    public string Version
    {
        get
        {
            try
            {
                return $"Version {WindowService.Version}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AboutViewModel] Version unavailable: {ex.Message}");
                return "Version —";
            }
        }
    }

    public string Blurb =>
        "My Stream Timer is an open source project by James Montemagno and Refractored LLC. " +
        "It writes countdowns, count-ups and the current time to text files so OBS, Streamlabs and other tools can show them live on stream.";

    public IReadOnlyList<LinkItem> Links { get; }

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [RelayCommand]
    private async Task OpenUrlAsync(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            await _launcher.OpenUriAsync(url);
        }
    }

    [RelayCommand]
    private async Task OpenLogsFolderAsync()
    {
        try
        {
            var logs = Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");
            Directory.CreateDirectory(logs);
            if (!await _launcher.OpenFolderAsync(logs))
            {
                StatusMessage = $"Couldn't open {logs}";
                IsStatusOpen = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AboutViewModel] OpenLogsFolder failed: {ex.Message}");
            StatusMessage = "Couldn't open the crash logs folder.";
            IsStatusOpen = true;
        }
    }

    [RelayCommand]
    private void CopyVersion() => _clipboard.SetText($"{AppName} {Version}");
}
