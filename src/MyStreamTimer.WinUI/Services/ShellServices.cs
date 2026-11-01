using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace MyStreamTimer.WinUI.Services;

/// <summary>Copies text to the system clipboard.</summary>
public sealed class ClipboardService
{
    public void SetText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClipboardService] {ex.Message}");
        }
    }
}

/// <summary>Opens URIs and folders with the shell.</summary>
public sealed class LauncherService
{
    public async Task<bool> OpenUriAsync(string uri)
    {
        try
        {
            var opened = await Launcher.LaunchUriAsync(new Uri(uri));
            if (!opened)
            {
                AppDiagnostics.Record("launcher-uri-not-opened", "The shell did not open the URI.");
            }
            return opened;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("launcher-uri-failed", "The shell URI launch failed.", ex);
            return false;
        }
    }

    public async Task<bool> OpenFolderAsync(string path)
    {
        try
        {
            var opened = await Launcher.LaunchFolderPathAsync(path);
            if (!opened)
            {
                AppDiagnostics.Record("launcher-folder-not-opened", "The shell did not open the folder.");
            }
            return opened;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("launcher-folder-failed", "The shell folder launch failed.", ex);
            return false;
        }
    }
}

/// <summary>Modal message and confirmation dialogs hosted in the main window.</summary>
public sealed class DialogService
{
    public async Task ShowMessageAsync(string title, string message, string closeText = "OK")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    public async Task<bool> ConfirmAsync(string title, string message, string primary = "OK", string secondary = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primary,
            CloseButtonText = secondary,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
