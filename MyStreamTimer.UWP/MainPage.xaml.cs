using System;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.UWP.Services;
using Windows.Foundation;
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

            var size = new Size(500, 430);
            ApplicationView.PreferredLaunchViewSize = size;

            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            ApplicationView.GetForCurrentView().SetPreferredMinSize(size);


        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            OnLaunchedEvent(e?.Parameter?.ToString());
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
