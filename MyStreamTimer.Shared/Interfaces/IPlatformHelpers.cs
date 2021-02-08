using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamTimer.Shared.Interfaces
{
    public interface IPlatformHelpers
    {
        void CopyToClipboard(string text);
        string BaseDirectory { get; }
        bool IsMac { get; }
        Task DisplayAlert(string title, string message);
        void OpenUrl(string url);

        void InvokeOnMainThread(Action action);

        void StoreReview();

        bool HasInternet { get; }
        void SetScreenSaver(bool s);
    }
}
