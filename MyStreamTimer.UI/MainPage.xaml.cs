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
        public static (bool start, float mins, string vm) OpeningArgs { get; set;  }
        public static TimerViewModel DownVM { get; set; }
        public static TimerViewModel DownVM2 { get; set; }
        public static TimerViewModel DownVM3 { get; set; }
        public static TimerViewModel DownVM4 { get; set; }
        public MainPage()
        {
            InitializeComponent();

            var first = OpeningArgs.vm == "countdown" || OpeningArgs.vm == "countdown1";
            var second = OpeningArgs.vm == "countdown2";
            var third = OpeningArgs.vm == "countdown3";
            var fourth = OpeningArgs.vm == "countdown4";

            TabItemDown.BindingContext = DownVM = new TimerViewModel(Constants.Countdown, first && OpeningArgs.start, first ? OpeningArgs.mins : -1);
            TabItemDown2.BindingContext = DownVM2 = new TimerViewModel(Constants.Countdown2, second && OpeningArgs.start, second ? OpeningArgs.mins : -1);
            TabItemDown3.BindingContext = DownVM3 = new TimerViewModel(Constants.Countdown3, third && OpeningArgs.start, third ? OpeningArgs.mins : -1);
            TabItemDown4.BindingContext = DownVM4 = new TimerViewModel(Constants.Countdown4, fourth && OpeningArgs.start && GlobalSettings.IsPro, fourth ? OpeningArgs.mins : -1);
            TabItemUp.BindingContext = new TimerViewModel(Constants.Countup);
            TabItemUp2.BindingContext = new TimerViewModel(Constants.Countup2);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var helper = ServiceContainer.Resolve<IPlatformHelpers>();
            if (GlobalSettings.TimesUsed < 10)
            {
                GlobalSettings.TimesUsed++;
                if (GlobalSettings.TimesUsed == 10)
                {
                    helper?.StoreReview();
                }
            
            }
            
            if(GlobalSettings.FirstRun && Device.RuntimePlatform == Device.UWP)
            {
                await DisplayAlert("Hello There!",
                    "Welcome to My Stream Timer 2.0! It has been a long road, but this is a significant update with awesome functionality requested by you! If you were using a previous version of My Stream Timer the file save locations have changed! :( They are now per user automatically. Please update OBS/SLOBS with this new file location.", "OK");
                GlobalSettings.FirstRun = false;
            }
            
            if (helper == null)
                return;

            helper.SetScreenSaver(GlobalSettings.StayOnTop);
        }
    }
}
