using Microsoft.Windows.Storage.Pickers;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.WinUI;
using MyStreamTimer.WinUI.Services;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace MyStreamTimer.WindowsStability.Tests
{
    // Source-linked FolderService; native picker/storage APIs are Task-based OS doubles below.
    [Collection("Windows stability")]
    public sealed class FolderServiceTests
    {
        public FolderServiceTests()
        {
            App.UiDispatcher = new(() => true, _ => false);
            AppDiagnostics.Records.Clear();
            FolderPicker.Constructions = 0;
            FolderPicker.Pick = () => Task.FromResult<PickedFolder?>(new("private-fixture-folder"));
            StorageFolder.Lookups = 0;
            StorageFolder.Open = path => Task.FromResult(new StorageFolder(path));
            StorageApplicationPermissions.FutureAccessList.Entries.Clear();
        }

        [Fact]
        public async Task Active_selection_remembers_the_selected_folder_and_returns_its_path()
        {
            var result = await new FolderService(new NullTimerPlatform()).PickFolderAsync();

            Assert.Equal("private-fixture-folder", result);
            Assert.Equal(1, FolderPicker.Constructions);
            Assert.Equal(1, StorageFolder.Lookups);
            var entry = Assert.Single(StorageApplicationPermissions.FutureAccessList.Entries);
            Assert.Equal(FolderService.FutureAccessToken, entry.Token);
            Assert.Equal(result, entry.Folder.Path);
            Assert.Empty(AppDiagnostics.Records);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Cancelled_or_empty_selection_does_not_lookup_or_remember_a_folder(string? path)
        {
            FolderPicker.Pick = () => Task.FromResult(path is null ? null : new PickedFolder(path));

            Assert.Null(await new FolderService(new NullTimerPlatform()).PickFolderAsync());
            Assert.Equal(0, StorageFolder.Lookups);
            Assert.Empty(StorageApplicationPermissions.FutureAccessList.Entries);
            Assert.Empty(AppDiagnostics.Records);
        }

        [Fact]
        public async Task Shutdown_before_selection_does_not_construct_a_picker()
        {
            App.UiDispatcher.Shutdown();

            Assert.Null(await new FolderService(new NullTimerPlatform()).PickFolderAsync());
            Assert.Equal(0, FolderPicker.Constructions);
            Assert.Equal(0, StorageFolder.Lookups);
            Assert.Empty(StorageApplicationPermissions.FutureAccessList.Entries);
            AssertShutdownDiagnostic();
        }

        [Fact]
        public async Task Shutdown_while_picker_is_pending_does_not_start_storage_lookup()
        {
            var pending = new TaskCompletionSource<PickedFolder?>(TaskCreationOptions.RunContinuationsAsynchronously);
            FolderPicker.Pick = () => pending.Task;
            var operation = new FolderService(new NullTimerPlatform()).PickFolderAsync();
            Assert.False(operation.IsCompleted);

            App.UiDispatcher.Shutdown();
            pending.SetResult(new("private-fixture-folder"));

            Assert.Null(await operation.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(0, StorageFolder.Lookups);
            Assert.Empty(StorageApplicationPermissions.FutureAccessList.Entries);
            AssertShutdownDiagnostic();
        }

        [Fact]
        public async Task Shutdown_while_storage_lookup_is_pending_does_not_change_future_access()
        {
            var pending = new TaskCompletionSource<StorageFolder>(TaskCreationOptions.RunContinuationsAsynchronously);
            StorageFolder.Open = _ => pending.Task;
            var operation = new FolderService(new NullTimerPlatform()).PickFolderAsync();
            Assert.Equal(1, StorageFolder.Lookups);
            Assert.False(operation.IsCompleted);

            App.UiDispatcher.Shutdown();
            pending.SetResult(new("private-fixture-folder"));

            Assert.Null(await operation.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Empty(StorageApplicationPermissions.FutureAccessList.Entries);
            AssertShutdownDiagnostic();
        }

        [Fact]
        public async Task Active_access_probe_creates_directory_and_removes_its_probe_file()
        {
            var path = NewFixturePath();
            try
            {
                var result = await new FolderService(new NullTimerPlatform()).TestAccessAsync(path);

                Assert.True(result.Ok);
                Assert.Equal(FolderService.AccessOkMessage, result.Message);
                Assert.True(Directory.Exists(path));
                Assert.Empty(Directory.EnumerateFileSystemEntries(path));
                Assert.Empty(AppDiagnostics.Records);
            }
            finally
            {
                if (Directory.Exists(path))
                    Directory.Delete(path);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Shutdown_before_entry_or_worker_start_does_not_create_probe_directory(bool atWorkerStart)
        {
            var path = NewFixturePath();
            ITimerPlatform platform = atWorkerStart ? new ShutdownOnActivityRead() : new NullTimerPlatform();
            if (!atWorkerStart)
                App.UiDispatcher.Shutdown();
            try
            {
                var result = await new FolderService(platform).TestAccessAsync(path);

                Assert.False(result.Ok);
                Assert.Equal("The app is shutting down; folder access was skipped.", result.Message);
                Assert.False(Directory.Exists(path));
                AssertShutdownDiagnostic();
            }
            finally
            {
                if (Directory.Exists(path))
                    Directory.Delete(path);
            }
        }

        [Fact]
        public async Task Running_timers_still_reject_access_without_creating_directory()
        {
            var platform = new NullTimerPlatform();
            platform.StartActivity("fixture-timer");
            var path = NewFixturePath();

            var result = await new FolderService(platform).TestAccessAsync(path);

            Assert.False(result.Ok);
            Assert.Equal(FolderService.TimersRunningMessage, result.Message);
            Assert.False(Directory.Exists(path));
            Assert.Empty(AppDiagnostics.Records);
        }

        [Fact]
        public async Task Existing_file_instead_of_directory_keeps_explicit_access_failure()
        {
            var path = NewFixturePath();
            File.WriteAllText(path, "fixture");
            try
            {
                var result = await new FolderService(new NullTimerPlatform()).TestAccessAsync(path);

                Assert.False(result.Ok);
                Assert.Equal(FolderService.AccessFailedMessage, result.Message);
                Assert.Equal("fixture", File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string NewFixturePath() =>
            Path.Combine(Path.GetTempPath(), "mst-folder-" + Guid.NewGuid().ToString("N"));

        private static void AssertShutdownDiagnostic()
        {
            var record = Assert.Single(AppDiagnostics.Records);
            Assert.Equal("folder-operation-skipped", record.Event);
            Assert.Equal("A folder operation was skipped because shutdown has started.", record.Details);
            Assert.Null(record.Exception);
        }

        private sealed class ShutdownOnActivityRead : ITimerPlatform
        {
            public bool HasRunningTimers
            {
                get { App.UiDispatcher.Shutdown(); return false; }
            }
            public void StartActivity(string id) { }
            public void StopActivity(string id) { }
            public Task BeepAsync(EndSoundSelection sound) => Task.CompletedTask;
        }
    }
}

#if WINDOWS_STABILITY_STUBS
namespace MyStreamTimer.WinUI
{
    internal static class App
    {
        internal static ShutdownDispatcher UiDispatcher { get; set; } = null!;
        internal static TestWindow Window { get; } = new();
        internal static nint WindowHandle => 0;
    }
    internal sealed class TestWindow
    {
        internal TestAppWindow AppWindow { get; } = new();
        internal Microsoft.UI.Xaml.Controls.TestContent Content { get; } = new();
    }
    internal sealed class TestAppWindow
    {
        internal int Id => 1;
    }
}

namespace Microsoft.Windows.Storage.Pickers
{
    public enum PickerLocationId { DocumentsLibrary }
    public sealed record PickedFolder(string Path);
    public sealed class FolderPicker
    {
        public static int Constructions;
        public static Func<Task<PickedFolder?>> Pick { get; set; } = null!;
        public FolderPicker(int windowId) => Constructions++;
        public PickerLocationId SuggestedStartLocation { get; set; }
        public Task<PickedFolder?> PickSingleFolderAsync() => Pick();
    }
}

namespace Windows.Storage
{
    public sealed record StorageFolder(string Path)
    {
        public static int Lookups;
        public static Func<string, Task<StorageFolder>> Open { get; set; } = null!;
        public static Task<StorageFolder> GetFolderFromPathAsync(string path)
        {
            Lookups++;
            return Open(path);
        }
    }
}

namespace Windows.Storage.AccessCache
{
    public static class StorageApplicationPermissions
    {
        public static TestAccessList FutureAccessList { get; } = new();
    }
    public sealed class TestAccessList
    {
        public List<(string Token, StorageFolder Folder)> Entries { get; } = [];
        public void AddOrReplace(string token, StorageFolder folder) => Entries.Add((token, folder));
    }
}
#endif
