using System;
using System.Threading.Tasks;
using AppKit;
using Foundation;
using MyStreamTimer.Shared.Interfaces;
using Xamarin.Forms;
using Xamarin.Essentials;

namespace MyStreamTimer.Mac.Services
{
    public class PlatformHelpers : IPlatformHelpers
    {
        public bool IsMac { get; } = true;
        public string BaseDirectory
        {
            get
            {
                var test = 
                 NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User);

                return test[0];
            }
        }

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
    }
}
