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
        public MainPage()
        {
            InitializeComponent();

            var first = OpeningArgs.vm == "countdown" || OpeningArgs.vm == "countdown1";
            var second = OpeningArgs.vm == "countdown2";
            var third = OpeningArgs.vm == "countdown3";

            TabItemDown.BindingContext = DownVM = new TimerViewModel(Constants.Countdown, first ? OpeningArgs.start : false, first ? OpeningArgs.mins : -1);
            TabItemDown2.BindingContext = DownVM2 = new TimerViewModel(Constants.Countdown2, second ? OpeningArgs.start : false, second ? OpeningArgs.mins : -1);
            TabItemDown3.BindingContext = DownVM3 = new TimerViewModel(Constants.Countdown3, third ? OpeningArgs.start : false, third ? OpeningArgs.mins : -1);
            TabItemUp.BindingContext = new TimerViewModel(Constants.Countup);

            
        }

        protected override void OnAppearing()
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
            if (helper == null)
                return;

            helper.SetScreenSaver(GlobalSettings.StayOnTop);
        }
    }
}
