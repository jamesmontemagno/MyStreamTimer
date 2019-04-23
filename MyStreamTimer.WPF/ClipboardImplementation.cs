using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.WPF
{
    public class ClipboardImplementation : IClipboard
    {
        public string BaseDirectory => 
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        public void CopyToClipboard(string text) => 
            Clipboard.SetText(text);

        public void OpenUrl(string url)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.FileName = url;
            process.Start();
        }
    }
}
