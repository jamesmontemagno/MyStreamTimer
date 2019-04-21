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
        public MainPage()
        {
            InitializeComponent();

            var start = false;
            var mins = -1;

            TabItemDown.BindingContext = new TimerViewModel(Constants.Countdown, start, mins);
            TabItemUp.BindingContext = new TimerViewModel(Constants.Countup);
            TabItemGiveaway.BindingContext = new TimerViewModel(Constants.Giveaway);
        }
    }
}
