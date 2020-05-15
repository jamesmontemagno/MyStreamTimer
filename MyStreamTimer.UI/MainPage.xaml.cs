using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyStreamTimer.Shared.Helpers;
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
    }
}
