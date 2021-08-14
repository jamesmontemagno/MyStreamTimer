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

namespace MyStreamTimer.Mac.Services
{
    public class PlatformHelpers : IPlatformHelpers
    {

        Dictionary<string, NSObject> Activities { get; } = new Dictionary<string, NSObject>();

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

        public void StartActivity(string id)
        {
            if (Activities.ContainsKey(id))
                return;

            try
            {
                var options = NSActivityOptions.UserInitiated | NSActivityOptions.IdleDisplaySleepDisabled;
                var activity = NSProcessInfo.ProcessInfo.BeginActivity(options, "User has inititiated a timer that is a long running process.");

                Activities.Add(id, activity);
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to start activity: {ex}");
            }
        }
    }
}
