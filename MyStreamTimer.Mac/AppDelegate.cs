using AppKit;
using Foundation;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.UI;
using Xamarin.Forms;
using Xamarin.Forms.Platform.MacOS;

namespace MyStreamTimer.Mac
{
    [Register("AppDelegate")]
    public class AppDelegate : FormsApplicationDelegate
    {
        NSWindow window;
        public AppDelegate()
        {
            var style = NSWindowStyle.Closable | NSWindowStyle.Titled;

            var rect = new CoreGraphics.CGRect(500, 300, 500, 300);
            window = new NSWindow(rect, style, NSBackingStore.Buffered, false);
            window.Title = "My Stream Timer"; // choose your own Title here
            window.TitleVisibility = NSWindowTitleVisibility.Visible;
        }

        public override NSWindow MainWindow => window;

        public override void DidFinishLaunching(NSNotification notification)
        {
            Forms.Init();

            ServiceContainer.Register<IClipboard>(() => new ClipboardImplementation());
            LoadApplication(new MyStreamTimer.UI.App());
            base.DidFinishLaunching(notification);
        }
    }
}
