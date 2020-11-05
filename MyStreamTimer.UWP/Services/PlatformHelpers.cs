using System;
using System.Threading.Tasks;
using MyStreamTimer.Shared.Interfaces;
using Xamarin.Forms;
using Xamarin.Essentials;
using Windows.Services.Store;
using System.Threading;
using Windows.Foundation;

namespace MyStreamTimer.UWP.Services
{
    public class PlatformHelpers : IPlatformHelpers
    {
        public bool IsMac => false;
        public string BaseDirectory =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

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

        public void StoreReview() => StoreRequestHelper.SendRequestAsync(StoreContext.GetDefault(), 16, string.Empty).WatchForError();

        public void InvokeOnMainThread(Action action)
        {
            Device.BeginInvokeOnMainThread(action);
        }
    }

    static partial class PlatformExtensions
    {
        internal static void WatchForError(this IAsyncAction self) =>
            self.AsTask().WatchForError();

        internal static void WatchForError<T>(this IAsyncOperation<T> self) =>
            self.AsTask().WatchForError();

        internal static void WatchForError(this Task self)
        {
            var context = SynchronizationContext.Current;
            if (context == null)
                return;

            self.ContinueWith(
                t =>
                {
                    var exception = t.Exception.InnerExceptions.Count > 1 ? t.Exception : t.Exception.InnerException;

                    context.Post(e => { throw (Exception)e; }, exception);
                }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
