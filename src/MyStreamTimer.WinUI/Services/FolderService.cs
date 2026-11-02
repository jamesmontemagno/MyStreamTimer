using System.Diagnostics;
using MyStreamTimer.Core.Services;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace MyStreamTimer.WinUI.Services;

/// <summary>Folder picking and output-directory validation (legacy "Test Access").</summary>
public sealed class FolderService
{
    public const string FutureAccessToken = "OutputFolder";
    public const string AccessOkMessage = "This directory is valid and can be accessed! New files will be saved here.";
    public const string AccessFailedMessage = "Double check this is a valid directory path, or make sure that My Stream Timer has full write access to put files in this directory.";
    public const string TimersRunningMessage = "Please stop all timers before changing the save directory";
    public const string ShutdownMessage = "The app is shutting down; folder access was skipped.";

    private readonly ITimerPlatform _platform;

    public FolderService(ITimerPlatform platform)
    {
        _platform = platform;
    }

    /// <summary>Shows the folder picker; returns the chosen path or null when cancelled.</summary>
    public async Task<string?> PickFolderAsync()
    {
        if (SkipForShutdown())
            return null;

        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(App.Window.AppWindow.Id)
            {
                SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            };

            var result = await picker.PickSingleFolderAsync();
            if (result is null || string.IsNullOrEmpty(result.Path))
            {
                return null;
            }

            if (SkipForShutdown())
                return null;

            // Keep parity with the legacy app: remember the folder in the Future Access List.
            var folder = await StorageFolder.GetFolderFromPathAsync(result.Path);
            if (SkipForShutdown())
                return null;

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(FutureAccessToken, folder);
            return result.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FolderService] PickFolder failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Creates the directory if needed and verifies a file can be written and deleted there.</summary>
    public Task<(bool Ok, string Message)> TestAccessAsync(string path)
    {
        if (SkipForShutdown())
            return Task.FromResult((false, ShutdownMessage));

        if (_platform.HasRunningTimers)
        {
            return Task.FromResult((false, TimersRunningMessage));
        }

        return Task.Run(() =>
        {
            if (SkipForShutdown())
                return (false, ShutdownMessage);

            // Once the probe starts, let its write/delete finish even if shutdown begins.
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return (false, AccessFailedMessage);
                }

                Directory.CreateDirectory(path);
                var probe = Path.Combine(path, Path.GetRandomFileName());
                File.WriteAllText(probe, "test");
                File.Delete(probe);
                return (true, AccessOkMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FolderService] TestAccess failed: {ex.Message}");
                return (false, AccessFailedMessage);
            }
        });
    }

    private static bool SkipForShutdown()
    {
        if (!App.UiDispatcher.IsShuttingDown)
            return false;

        AppDiagnostics.Record("folder-operation-skipped", "A folder operation was skipped because shutdown has started.");
        return true;
    }
}
