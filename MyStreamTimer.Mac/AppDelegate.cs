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
           
            
            var appleEventManager = NSAppleEventManager.SharedAppleEventManager;

            appleEventManager.SetEventHandler(this, new ObjCRuntime.Selector("handleGetURLEvent:withReplyEvent:"), AEEventClass.Internet, AEEventID.GetUrl);

            base.DidFinishLaunching(notification);
        }

        [Export("handleGetURLEvent:withReplyEvent:")]
        private void HandleGetURLEvent(NSAppleEventDescriptor descriptor, NSAppleEventDescriptor replyEvent)
        {
            // Breakpoint here, debug normally and *then* call your URL
            var keyDirectObject = "----";
            var keyword = (((uint)keyDirectObject[0]) << 24 |
                           ((uint)keyDirectObject[1]) << 16 |
                           ((uint)keyDirectObject[2]) << 8 |
                           ((uint)keyDirectObject[3]));
            var urlString = descriptor.ParamDescriptorForKeyword(keyword).StringValue;
        }

        public override void OpenUrls(NSApplication application, NSUrl[] urls)
        {
            
            base.OpenUrls(application, urls);
        }


    }
}
