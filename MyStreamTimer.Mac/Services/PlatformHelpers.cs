using System;
using System.Threading.Tasks;
using AppKit;
using Foundation;
using MyStreamTimer.Shared.Interfaces;
using Xamarin.Forms;
using Xamarin.Essentials;
using StoreKit;
using System.Collections.Generic;
using System.Diagnostics;
using MyStreamTimer.Shared.Helpers;

namespace MyStreamTimer.Mac.Services
{
    public class PlatformHelpers : IPlatformHelpers
    {

        public bool HasRunningTimers => Activities.Count > 0;

        
        public bool IsMac => true;
        public string BaseDirectory
        {
            get
            {
                var test = 
                 NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User);

                return test[0];
            }
        }

        public bool HasInternet => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public void CopyToClipboard(string text) =>
            Clipboard.SetTextAsync(text).ContinueWith( _ => { });

        public void OpenUrl(string url) =>
            Browser.OpenAsync(url).ContinueWith( _ => { });

        public Task DisplayAlert(string title, string message)
        {
            if (Application.Current.MainPage is Page page && page != null)
            {
                return page.DisplayAlert(title, message, "OK");
            }

            return Task.CompletedTask;
        }

        public void StoreReview()
        {
            if(DeviceInfo.Version >= new Version(10, 14))
            {
                SKStoreReviewController.RequestReview();
            }
        }

        public void InvokeOnMainThread(Action action)
        {
            Xamarin.Forms.Device.BeginInvokeOnMainThread(action);
        }

        public void SetScreenSaver(bool s)
        {
            ((AppDelegate)NSApplication.SharedApplication.Delegate).MainWindow.Level = s ? NSWindowLevel.ScreenSaver : NSWindowLevel.Normal;
        }

        public async Task Beep()
        {
            try
            {
                AppKitFramework.NSBeep();
                await Task.Delay(200);
                AppKitFramework.NSBeep();
                await Task.Delay(200);
                AppKitFramework.NSBeep();
            }
            catch (Exception ex)
            {

            }
        }

        Dictionary<string, NSObject> Activities { get; } = new Dictionary<string, NSObject>();

        public void StartActivity(string id)
        {
            if (Activities.ContainsKey(id))
                return;

            try
            {
                var options = NSActivityOptions.UserInitiated | NSActivityOptions.IdleDisplaySleepDisabled;
                var activity = NSProcessInfo.ProcessInfo.BeginActivity(options, "User has inititiated a timer that is a long running process.");

                Activities.Add(id, activity);
                if (Activities.Count == 1)
                    StartBookmark();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to start activity: {ex}");
            }
        }

        public void StopActivity(string id)
        {
            if (!Activities.ContainsKey(id))
                return;
            try
            {
                var activity = Activities[id];
                NSProcessInfo.ProcessInfo.EndActivity(activity);
                Activities.Remove(id);

                if (Activities.Count == 0)
                    StopBookmark();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to start activity: {ex}");
            }
        }

        public void StartBookmark(bool showException = true)
        {
            try
            {
                if (GlobalSettings.DirectoryPath == BaseDirectory)
                    return;

                var bookmark = NSUserDefaults.StandardUserDefaults.DataForKey("bookmark");
                if (bookmark != null)
                {
                    var url2 = NSUrl.FromBookmarkData(bookmark, NSUrlBookmarkResolutionOptions.WithSecurityScope, null, out var isStale, out var error3);

                    if (error3 != null)
                        throw new Exception(error3.LocalizedDescription);
                    if (isStale)
                    {
                        var data = url2.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, null, null, out var error2);
                        if(error2 != null)
                            throw new Exception(error2.LocalizedDescription);

                        NSUserDefaults.StandardUserDefaults["bookmark"] = data;
                        NSUserDefaults.StandardUserDefaults.Synchronize();
                    }
                    url2.StartAccessingSecurityScopedResource();
                }
            }
            catch (Exception ex)
            {
                if (!showException)
                    return;

                InvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert("Access error", $"Unable to access save folder, please re-pick the save location on the about tab. Error Code: {ex.Message}", "OK");
                });
            }
        }

        public void StopBookmark(bool showException = true)
        {
            try
            {
                if (GlobalSettings.DirectoryPath == BaseDirectory)
                    return;

                var bookmark = NSUserDefaults.StandardUserDefaults.DataForKey("bookmark");
                if (bookmark != null)
                {
                    var url2 = NSUrl.FromBookmarkData(bookmark, NSUrlBookmarkResolutionOptions.WithSecurityScope, null, out var isStale, out var error3);
                    url2.StopAccessingSecurityScopedResource();
                    if (isStale)
                    {
                        var data = url2.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, null, null, out var error2);
                        NSUserDefaults.StandardUserDefaults["bookmark"] = data;
                        NSUserDefaults.StandardUserDefaults.Synchronize();
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public bool WriteFileNative(string directory)
        {
            try
            {
                if (GlobalSettings.DirectoryPath != directory)
                    StartBookmark();

                var random = System.IO.Path.GetRandomFileName();
                var url = NSUrl.FromString(directory).Append(random, false).ToString();
                var fm = NSFileManager.DefaultManager;
                NSDictionary dict = null;
                fm.CreateFile(url, NSData.FromString("test"), dict);

                fm.Remove(url, out var error);

                if (System.IO.File.Exists(url))
                    System.IO.File.Delete(url);

                if (GlobalSettings.DirectoryPath != directory)
                    StopBookmark();
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public async Task<string> PickFolder()
        {
            try
            {

                var panel = NSOpenPanel.OpenPanel;

                panel.CanChooseDirectories = true;
                panel.CanChooseFiles = false;
                panel.CanCreateDirectories = false;
                panel.CanSelectHiddenExtension = false;
                panel.Title = "Select folder";
                panel.AllowsMultipleSelection = false;
                
                var tcs = new TaskCompletionSource<string>();

                panel.BeginSheet(NSApplication.SharedApplication.MainWindow, (result) =>
                {
                    if(result == (int)NSModalResponse.OK)
                    {
                        if (panel.Url != null)
                        {
                            var dir = panel.Url.RelativePath;

                            //only get bookmark if not in the container
                            if (GlobalSettings.DirectoryPath != dir)
                            {
                                var data = panel.Url.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, null, null, out var error);

                                if (data != null)
                                {
                                    NSUserDefaults.StandardUserDefaults["bookmark"] = data;
                                    NSUserDefaults.StandardUserDefaults.Synchronize();
                                }
                            }

                            tcs.SetResult(dir);
                        }
                        else
                        {
                            tcs.SetResult(string.Empty);
                        }
                    }
                    else
                    {
                        tcs.SetResult(string.Empty);
                    }

                    panel.Close();

                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }

            
        }
    }
}
