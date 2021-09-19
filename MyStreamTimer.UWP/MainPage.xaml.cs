using System;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.UWP.Services;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Navigation;

// csharpfritz gifted 10 subs on August 21st 2020
// adenearnshaw cheered 100 bits on August 21st 2020

namespace MyStreamTimer.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();

            ServiceContainer.Register<IPlatformHelpers>(() => new PlatformHelpers());
            LoadApplication(new UI.App());

            var size = new Size(600, 480);
            ApplicationView.PreferredLaunchViewSize = size;

            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            ApplicationView.GetForCurrentView().SetPreferredMinSize(size);


        }

        private ExtendedExecutionSession session = null;
        void ClearExtendedExecution()
        {
            if (session != null)
            {
                session.Revoked -= SessionRevoked;
                session.Dispose();
                session = null;
            }
        }

        private async void BeginExtendedExecution()
        {
            // The previous Extended Execution must be closed before a new one can be requested.
            // This code is redundant here because the sample doesn't allow a new extended
            // execution to begin until the previous one ends, but we leave it here for illustration.
            ClearExtendedExecution();

            var newSession = new ExtendedExecutionSession();
            newSession.Reason = ExtendedExecutionReason.Unspecified;
            newSession.Description = "Running time in the background";
            newSession.Revoked += SessionRevoked;
            var result = await newSession.RequestExtensionAsync();

            switch (result)
            {
                case ExtendedExecutionResult.Allowed:
                    session = newSession;
                    break;

                default:
                case ExtendedExecutionResult.Denied:
                    newSession.Dispose();
                    break;
            }
        }
        private void EndExtendedExecution()
        {
            ClearExtendedExecution();
        }

        private async void SessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (args.Reason)
                {
                    case ExtendedExecutionRevokedReason.Resumed:
                        break;

                    case ExtendedExecutionRevokedReason.SystemPolicy:
                        break;
                }

                EndExtendedExecution();
                BeginExtendedExecution();
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            OnLaunchedEvent(e?.Parameter?.ToString());
            BeginExtendedExecution();
        }

        public void OnLaunchedEvent(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
                return;

            try
            {
                Xamarin.Forms.Application.Current.SendOnAppLinkRequestReceived(new Uri(arguments));
            }
            catch
            {

            }
            
        }

    }
}
