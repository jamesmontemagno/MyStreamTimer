using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyStreamTimer.WinUI.Services;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Windows.Storage.Pickers;

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

    private readonly LauncherService _launcher;
    private readonly ClipboardService _clipboard;

    public AboutViewModel(LauncherService launcher, ClipboardService clipboard)
    {
        _launcher = launcher;
        _clipboard = clipboard;

        Links =
        [
            new("Source on GitHub", "\uE943", RepositoryUrl, OpenUrlCommand),
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
        "My Stream Timer is an open source project. " +
        "It writes countdowns, count-ups and the current time to text files so OBS, Streamlabs and other tools can show them live on stream.";

    public IReadOnlyList<LinkItem> Links { get; }

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

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
            var logs = AppDiagnostics.DirectoryPath;
            if (string.IsNullOrEmpty(logs))
            {
                AppDiagnostics.Record("diagnostics-folder-unavailable");
                StatusMessage = "Local diagnostics are unavailable. Try restarting the app.";
                StatusSeverity = InfoBarSeverity.Error;
                IsStatusOpen = true;
                return;
            }
            Directory.CreateDirectory(logs);
            if (!await _launcher.OpenFolderAsync(logs))
            {
                StatusMessage = $"Couldn't open {logs}";
                StatusSeverity = InfoBarSeverity.Error;
                IsStatusOpen = true;
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("diagnostics-folder-failed", exception: ex);
            StatusMessage = "Couldn't open the crash logs folder.";
            StatusSeverity = InfoBarSeverity.Error;
            IsStatusOpen = true;
        }
    }

    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        IsStatusOpen = false;
        try
        {
            var picker = new FileSavePicker(App.Window.AppWindow.Id)
            {
                SuggestedFileName = $"MyStreamTimer-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add("ZIP archive", new List<string> { ".zip" });
            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;
            AppDiagnostics.Record("diagnostics-export");
            var result = await Task.Run(() =>
            {
                var saved = AppDiagnostics.TryExport(file.Path, out var error);
                return (Saved: saved, Error: error);
            });
            StatusMessage = result.Saved
                ? "Report saved. Review it before sharing privately with support."
                : result.Error ?? "Couldn't save the diagnostics report.";
            StatusSeverity = result.Saved ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            IsStatusOpen = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or COMException or InvalidOperationException)
        {
            AppDiagnostics.Record("diagnostics-export-failed", exception: ex);
            StatusMessage = "Couldn't save the report. Choose a writable local folder and try again.";
            StatusSeverity = InfoBarSeverity.Error;
            IsStatusOpen = true;
        }
    }

    [RelayCommand]
    private void CopyVersion() => _clipboard.SetText($"{AppName} {Version}");
}
