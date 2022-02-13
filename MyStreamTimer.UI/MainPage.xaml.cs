using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.ViewModel;
using Plugin.InAppBilling;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using static MyStreamTimer.Shared.Helpers.Utils;

namespace MyStreamTimer.UI
{
   
    public partial class MainPage : TabbedPage
    {
        public static (CommandAction action, float mins, string vm) OpeningArgs { get; set;  }
        public static TimerViewModel DownVM { get; set; }
        public static TimerViewModel DownVM2 { get; set; }
        public static TimerViewModel DownVM3 { get; set; }
        public static TimerViewModel DownVM4 { get; set; }
        public static TimerViewModel UpVM { get; set; }
        public static TimerViewModel UpVM2 { get; set; }
        public MainPage()
        {
            InitializeComponent();

            var first = OpeningArgs.vm == "countdown" || OpeningArgs.vm == "countdown1";
            var second = OpeningArgs.vm == "countdown2";
            var third = OpeningArgs.vm == "countdown3";
            var fourth = OpeningArgs.vm == "countdown4";
            var up = OpeningArgs.vm == "countup" || OpeningArgs.vm == "countup1";
            var up2 = OpeningArgs.vm == "countup2";

            TabItemDown.BindingContext = DownVM = new TimerViewModel(Constants.Countdown, first && OpeningArgs.action == CommandAction.Start, first ? OpeningArgs.mins : -1);
            TabItemDown2.BindingContext = DownVM2 = new TimerViewModel(Constants.Countdown2, second && OpeningArgs.action == CommandAction.Start, second ? OpeningArgs.mins : -1);
            TabItemDown3.BindingContext = DownVM3 = new TimerViewModel(Constants.Countdown3, third && OpeningArgs.action == CommandAction.Start, third ? OpeningArgs.mins : -1);
            TabItemDown4.BindingContext = DownVM4 = new TimerViewModel(Constants.Countdown4, fourth && OpeningArgs.action == CommandAction.Start && GlobalSettings.IsPro, fourth ? OpeningArgs.mins : -1);
            TabItemUp.BindingContext = UpVM = new TimerViewModel(Constants.Countup, up && OpeningArgs.action == CommandAction.Start, up ? OpeningArgs.mins : -1);
            TabItemUp2.BindingContext = UpVM2 = new TimerViewModel(Constants.Countup2, up2 && OpeningArgs.action == CommandAction.Start && GlobalSettings.IsPro, up2 ? OpeningArgs.mins : -1);
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

            if (helper == null)
                return;

            if (helper.IsMac && GlobalSettings.CheckSubStatus && GlobalSettings.HasTippedSub && !GlobalSettings.IsSubValid)
            {
                GlobalSettings.CheckSubStatus = false;
                if (await DisplayAlert("Subcription Status Refresh", "Looks like it is time to update your subscription status. If you canceled and resumed, you can always refresh your status on the settings page by restoring purchases.", "Refresh status", "Maybe Later"))
                {
                    if (!helper.HasInternet)
                    {
                        GlobalSettings.CheckSubStatus = true;
                        await DisplayAlert("No internet", "Please check internet connection and try restore purchases again in settings.", "OK");
                        return;
                    }
                    //refresh status here
                    await RestorePurchases();
                }
            }

            /*if(GlobalSettings.FirstRun && Device.RuntimePlatform == Device.UWP)
            {
                await DisplayAlert("Hello There!",
                    "Welcome to My Stream Timer 2.0! It has been a long road, but this is a significant update with awesome functionality requested by you! If you were using a previous version of My Stream Timer the file save locations have changed! :( They are now per user automatically. Please update OBS/SLOBS with this new file location.", "OK");
                GlobalSettings.FirstRun = false;
            }*/

           

            helper.SetScreenSaver(GlobalSettings.StayOnTop);
        }

        async Task RestorePurchases()
        {
            
            try
            {

                var connected = await CrossInAppBilling.Current.ConnectAsync();
                if (!connected)
                    return;


                var foundStuff = false;

                var subs = await CrossInAppBilling.Current.GetPurchasesAsync(ItemType.Subscription);

                if (subs?.Any(p => p.ProductId == ProViewModel.SubId) ?? false)
                {
                    var sorted = subs.Where(p => p.ProductId == ProViewModel.SubId).OrderByDescending(i => i.TransactionDateUtc).ToList();
                    var recentSub = sorted[0];
                    if (recentSub != null)
                    {
                        
                        if (recentSub.TransactionDateUtc.AddSubTime() > DateTime.UtcNow)
                        {
                            foundStuff = true;
                            GlobalSettings.HasTippedSub = true;
                            GlobalSettings.CheckSubStatus = true;
                            GlobalSettings.SubExpirationDate = recentSub.TransactionDateUtc.AddSubTime();
                        }
                    }
                }

                if (!foundStuff)
                {
                    await DisplayAlert("Hmmmm!", $"Looks like we couldn't find any subscription renewals, check your purchases and restore them in settings. Don't worry, all of your ride data will be saved.", "OK");
                }
                else
                {
                    await DisplayAlert("Status Refreshed!", $"Thanks for being awesome and subscribing for another month of My Stream Timer Pro!", "OK");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Issue connecting: " + ex);
                await DisplayAlert("Uh Oh!", $"Looks like something has gone wrong, please check connection and restore in the settings.  Code: {ex.Message}", "OK");
            }
            finally
            {
                await CrossInAppBilling.Current.DisconnectAsync();
            }
        }
    }
}
