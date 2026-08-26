using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamTimer.Shared.Interfaces
{
    public interface IPlatformHelpers
    {
        string Version { get; }
        void CopyToClipboard(string text);
        string BaseDirectory { get; }
        bool IsMac { get; }
        Task DisplayAlert(string title, string message);
        void OpenUrl(string url);

        void InvokeOnMainThread(Action action);

        void StoreReview();

        Task Beep();

        bool HasInternet { get; }
        void SetScreenSaver(bool s);

        void StartActivity(string id);
        void StopActivity(string id);
        void StartBookmark(bool showException = true);
        void StopBookmark(bool showException = true);
        bool WriteFileNative(string directory);
        Task<string> PickFolder();
        bool HasRunningTimers { get; }
    }
}
