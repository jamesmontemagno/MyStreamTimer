using System;
using System.Threading.Tasks;
using MyStreamTimer.Shared.Interfaces;
using Xamarin.Forms;
using Xamarin.Essentials;
using Windows.Services.Store;
using System.Threading;
using Windows.Foundation;
using Windows.Storage.Streams;
using System.Collections.Generic;

namespace MyStreamTimer.UWP.Services
{
    public class PlatformHelpers : IPlatformHelpers
    {
        public bool IsMac => false;
        public string BaseDirectory =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        public bool HasInternet => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public bool HasRunningTimers => Activities.Count > 0;

        public void CopyToClipboard(string text) =>
            Clipboard.SetTextAsync(text).ContinueWith(_ => { });

        public void OpenUrl(string url) =>
            Browser.OpenAsync(url).ContinueWith(_ => { });

        public Task DisplayAlert(string title, string message)
        {
            if (Application.Current.MainPage is Page page && page != null)
            {
                return page.DisplayAlert(title, message, "OK");
            }

            return Task.CompletedTask;
        }

        public void StoreReview() => StoreRequestHelper.SendRequestAsync(StoreContext.GetDefault(), 16, string.Empty).WatchForError();

        public void InvokeOnMainThread(Action action) => Device.BeginInvokeOnMainThread(action);

        public void SetScreenSaver(bool s)
        {

        }

        public async Task Beep()
        {
            await Xamarin.Essentials.MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {


                    var mediaElement1 = new Windows.UI.Xaml.Controls.MediaElement();
                    var beepStream = await BeepBeep(200, 2000, 75);
                    mediaElement1.SetSource(beepStream, string.Empty);
                    mediaElement1.Play();
                    await Task.Delay(200);
                    mediaElement1.Play();
                    await Task.Delay(200);
                    mediaElement1.Play();
                }
                catch (Exception ex)
                {

                }
            });
        }

        public async Task<IRandomAccessStream> BeepBeep(int Amplitude, int Frequency, int Duration)
        {
            var A = ((Amplitude * (System.Math.Pow(2, 15))) / 1000) - 1;
            var DeltaFT = 2 * Math.PI * Frequency / 44100.0;

            var Samples = 441 * Duration / 10;
            var Bytes = Samples * 4;
            int[] Hdr = { 0X46464952, 36 + Bytes, 0X45564157, 0X20746D66, 16, 0X20001, 44100, 176400, 0X100004, 0X61746164, Bytes };

            var ims = new InMemoryRandomAccessStream();
            var outStream = ims.GetOutputStreamAt(0);
            var dw = new DataWriter(outStream);
            dw.ByteOrder = ByteOrder.LittleEndian;

            for (var I = 0; I < Hdr.Length; I++)
            {
                dw.WriteInt32(Hdr[I]);
            }
            for (var T = 0; T < Samples; T++)
            {
                var Sample = System.Convert.ToInt16(A * Math.Sin(DeltaFT * T));
                dw.WriteInt16(Sample);
                dw.WriteInt16(Sample);
            }
            await dw.StoreAsync();
            await outStream.FlushAsync();
            return ims;
        }

        List<string> Activities { get; } = new List<string>();
        public void StartActivity(string id)
        {
            if (Activities.Contains(id))
                return;

            Activities.Add(id);
        }
        public void StopActivity(string id)
        {
            if (!Activities.Contains(id))
                return;

            Activities.Remove(id);
        }

        public bool WriteFileNative(string directory) => false;
        public Task<string> PickFolder() => Task.FromResult(string.Empty);
        public void StartBookmark(bool showException = true)
        {

        }
        public void StopBookmark(bool showException = true)
        {

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
