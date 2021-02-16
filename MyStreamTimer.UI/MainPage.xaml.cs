using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.ViewModel;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MyStreamTimer.UI
{
   
    public partial class MainPage : TabbedPage
    {
        public static (bool start, float mins) OpeningArgs { get; set;  }
        public static TimerViewModel DownVM { get; set; }
        public MainPage()
        {
            InitializeComponent();
            TabItemDown.BindingContext = DownVM = new TimerViewModel(Constants.Countdown, OpeningArgs.start, OpeningArgs.mins);
            TabItemDown2.BindingContext = new TimerViewModel(Constants.Countdown2);
            TabItemDown3.BindingContext = new TimerViewModel(Constants.Countdown3);
            TabItemUp.BindingContext = new TimerViewModel(Constants.Countup);

            
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (GlobalSettings.TimesUsed < 20)
            {
                GlobalSettings.TimesUsed++;
                if (GlobalSettings.TimesUsed == 20)
                {
                    ServiceContainer.Resolve<IPlatformHelpers>()?.StoreReview();
                }
            }

            if(GlobalSettings.FirstRun && Device.RuntimePlatform == Device.UWP)
            {
                await DisplayAlert("Hello There!",
                    "Welcome to My Stream Timer 2.0! It has been a long road, but this is a significant update with awesome functionality requested by you! If you were using a previous version of My Stream Timer the file save locations have changed! :( They are now per user automatically. Please update OBS/SLOBS with this new file location.", "OK");
                GlobalSettings.FirstRun = false;
            }
        }
    }
}
