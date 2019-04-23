using System;
using System.Collections.Generic;
using System.Text;

namespace MyStreamTimer.Shared.Interfaces
{
    public interface IClipboard
    {
        void CopyToClipboard(string text);
        string BaseDirectory { get; }

        void OpenUrl(string url);
    }
}
