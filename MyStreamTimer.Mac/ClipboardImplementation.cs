using System;
using AppKit;
using Foundation;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.Mac
{
    public class ClipboardImplementation : IClipboard
    {
        static readonly string pasteboardType = NSPasteboard.NSPasteboardTypeString;
        static readonly string[] pasteboardTypes = { pasteboardType };
        static NSPasteboard Pasteboard => NSPasteboard.GeneralPasteboard;

        public string BaseDirectory
        {
            get
            {
                var test = 
                 NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User);

                return test[0];
            }
        }

        public void CopyToClipboard(string text)
        {
            Pasteboard.DeclareTypes(pasteboardTypes, null);
            Pasteboard.ClearContents();
            Pasteboard.SetStringForType(text, pasteboardType);
        }
    }
}
