using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.WPF
{
    public class ClipboardImplementation : IClipboard
    {
        public void CopyToClipboard(string text) => Clipboard.SetText(text);
    }
}
