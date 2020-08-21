using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.WPF
{
    public class ClipboardImplementation : IPlatformHelpers
    {
        public string BaseDirectory => 
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        public bool IsMac => false;

        public void CopyToClipboard(string text) => 
            Clipboard.SetText(text);

        public Task DisplayAlert(string title, string message)
        {
            MessageBox.Show(message, title);
            return Task.CompletedTask;
        }

        public void InvokeOnMainThread(Action action)
        {
            action();
        }

        public void OpenUrl(string url)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.FileName = url;
            process.Start();
        }

        public void StoreReview()
        {
            
        }
    }
}
