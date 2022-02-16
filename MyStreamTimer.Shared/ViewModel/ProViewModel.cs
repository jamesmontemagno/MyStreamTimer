using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MvvmHelpers;
using MvvmHelpers.Commands;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using Plugin.InAppBilling;

namespace MyStreamTimer.Shared.ViewModel
{
    public class ProViewModel : BaseViewModel
    {
        public const string SubId = "mstsub";
        public const string SubId6Months = "mstsub6months";
        public AsyncCommand<string> BuyCommand { get; }
        public AsyncCommand RestoreCommand { get; }
        IPlatformHelpers platformHelpers;
        public ProViewModel()
        {
            OpenUrlCommand = new Command<string>(ExecuteOpenUrlCommand);
            platformHelpers = ServiceContainer.Resolve<IPlatformHelpers>();
            BuyCommand = new AsyncCommand<string>(PurchasePro);
            RestoreCommand = new AsyncCommand(RestorePurchases);

#if DEBUG
            CrossInAppBilling.Current.InTestingMode = true;
#endif
        }
        public ICommand OpenUrlCommand { get; }
        void ExecuteOpenUrlCommand(string url)
        {
            var platform = ServiceContainer.Resolve<IPlatformHelpers>();
            if (platform == null)
                throw new Exception("Platform Helpers must be implemented");

            platform.OpenUrl(url);
        }

        public string ProPrice
        {
            get
            {
                if (string.IsNullOrWhiteSpace(GlobalSettings.ProPrice) || GlobalSettings.IsPro)
                    return "PRO";

                return $"PRO - {GlobalSettings.ProPrice}";
            }
        }

        public string SubPrice
        {
            get
            {
                if (string.IsNullOrWhiteSpace(GlobalSettings.SubPrice) || GlobalSettings.IsPro)
                    return "PRO SUBSCRIPTIONS";

                return $"PRO SUBSCRIPTION - {GlobalSettings.SubPrice} a month | {GlobalSettings.SubPrice6Months} for 6 months";
            }
        }


        public string SubStatus
        {
            get
            {
                if (GlobalSettings.HasTippedSub)
                {
                    if (GlobalSettings.IsSubValid)
                        return $"Valid Pro Subscription - {GlobalSettings.SubExpirationDate.ToLocalTime().ToShortDateString()}";
                    else
                        return "Subscription expired";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public bool IsBronze => GlobalSettings.IsBronze;
        public bool IsNotBronze => !IsBronze && !platformHelpers.IsMac;

        public bool IsSilver => GlobalSettings.IsSilver;
        public bool IsNotSilver => !IsSilver && !platformHelpers.IsMac;

        public bool IsGold => GlobalSettings.IsGold;
        public bool IsNotGold => !IsGold;

        public bool IsSubscribed => GlobalSettings.HasTippedSub && GlobalSettings.IsSubValid;
        public bool IsNotSubscribed => !IsSubscribed && platformHelpers.IsMac;

        async Task PurchasePro(string productId)
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {

                if (!platformHelpers.HasInternet)
                {
                    await platformHelpers.DisplayAlert("No internet", "Please check internet connection and try purchase again.");
                    return;
                }
                //Check Offline

                var connected = await CrossInAppBilling.Current.ConnectAsync();

                if (!connected)
                {
                    await platformHelpers.DisplayAlert("Unable to Connect", "Unable to connect to the app store, check your internet connectivity and try again.");
                    return;
                }

                IsBusy = true;
                platformHelpers.SetScreenSaver(false);
                //CellPro.IsEnabled = false;
                //CellRestore.IsEnabled = false;
                //check purchases
                var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, (productId == SubId || productId == SubId6Months) ? ItemType.Subscription : ItemType.InAppPurchase);

                if (purchase == null)
                {
                    return;
                }
                else if (purchase.State == PurchaseState.Purchased)
                {
                    if (productId == "mstbronze")
                    {
                        GlobalSettings.IsBronze = true;
                        OnPropertyChanged(nameof(IsBronze));
                        OnPropertyChanged(nameof(IsNotBronze));
                    }
                    else if (productId == "mstsilver")
                    {
                        GlobalSettings.IsSilver = true;
                        OnPropertyChanged(nameof(IsSilver));
                        OnPropertyChanged(nameof(IsNotSilver));
                    }
                    else if (productId == "mstgold")
                    {
                        GlobalSettings.IsGold = true;
                        OnPropertyChanged(nameof(IsGold));
                        OnPropertyChanged(nameof(IsNotGold));
                    }
                    else if (productId == SubId)
                    {
                        GlobalSettings.SubExpirationDate = DateTime.UtcNow.AddSubTime();
                        GlobalSettings.HasTippedSub = true;
                        GlobalSettings.CheckSubStatus = true;
                        OnPropertyChanged(nameof(IsSubscribed));
                        OnPropertyChanged(nameof(IsNotSubscribed));
                        OnPropertyChanged(nameof(SubStatus));
                    }
                    else if (productId == SubId6Months)
                    {
                        GlobalSettings.SubExpirationDate = DateTime.UtcNow.AddSubTime(6);
                        GlobalSettings.HasTippedSub = true;
                        GlobalSettings.CheckSubStatus = true;
                        OnPropertyChanged(nameof(IsSubscribed));
                        OnPropertyChanged(nameof(IsNotSubscribed));
                        OnPropertyChanged(nameof(SubStatus));
                    }
                    return;
                }

                throw new InAppBillingPurchaseException(PurchaseError.GeneralError);

            }
            catch (InAppBillingPurchaseException purchaseEx)
            {
                var message = string.Empty;
                switch (purchaseEx.PurchaseError)
                {
                    case PurchaseError.AppStoreUnavailable:
                        message = "Currently the app store seems to be unavailble. Try again later.";
                        break;
                    case PurchaseError.BillingUnavailable:
                        message = "Billing seems to be unavailable, please try again later.";
                        break;
                    case PurchaseError.PaymentInvalid:
                        message = "Payment seems to be invalid, please try again.";
                        break;
                    case PurchaseError.PaymentNotAllowed:
                        message = "Payment does not seem to be enabled/allowed, please try again.";
                        break;
                    case PurchaseError.UserCancelled:
                        break;
                    default:
                        message = "Something has gone wrong, please try again.";
                        break;
                }

                if (string.IsNullOrWhiteSpace(message))
                    return;

                Console.WriteLine("Issue connecting: " + purchaseEx);
                await platformHelpers.DisplayAlert("Uh Oh!", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Issue connecting: " + ex);
                await platformHelpers.DisplayAlert("Uh Oh!", $"Looks like something has gone wrong, please try again. Code: {ex.Message}");
            }
            finally
            {
                await CrossInAppBilling.Current.DisconnectAsync();
                IsBusy = false;

                platformHelpers.SetScreenSaver(GlobalSettings.StayOnTop);
                //IsBuying = false;

                //CellPro.IsEnabled = true;
                //CellRestore.IsEnabled = true;
            }
        }


        int trycount = 0;
        async Task RestorePurchases()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                if (!platformHelpers.HasInternet)
                {
                    await platformHelpers.DisplayAlert("No internet", "Please check internet connection and try purchase again.");
                    return;
                }

                //Check Offline


                var connected = await CrossInAppBilling.Current.ConnectAsync();

                if (!connected)
                {
                    await platformHelpers.DisplayAlert("Unable to Connect", "Unable to connect to the app store, check your internet connectivity and try again.");
                    return;
                }


                //CellPro.IsEnabled = false;
                //CellRestore.IsEnabled = false;

                //check purchases
                trycount++;
                var purchases = await CrossInAppBilling.Current.GetPurchasesAsync(ItemType.InAppPurchase);

                var found = false;
                if (purchases?.Any(p => p.ProductId == "mstgold") ?? false)
                {

                    //Purchase restored
                    GlobalSettings.IsGold = true;

                    OnPropertyChanged(nameof(IsGold));
                    OnPropertyChanged(nameof(IsNotGold));
                    found = true;

                }

                if (purchases?.Any(p => p.ProductId == "mstbronze") ?? false)
                {

                    //Purchase restored
                    GlobalSettings.IsBronze = true;

                    OnPropertyChanged(nameof(IsBronze));
                    OnPropertyChanged(nameof(IsNotBronze));
                    found = true;

                }

                if (purchases?.Any(p => p.ProductId == "mstsilver") ?? false)
                {

                    //Purchase restored
                    GlobalSettings.IsSilver = true;

                    OnPropertyChanged(nameof(IsSilver));
                    OnPropertyChanged(nameof(IsNotSilver));
                    found = true;

                }

               
                if (purchases?.Any(p => p.ProductId == SubId) ?? false)
                {
                    var sorted = purchases.Where(p => p.ProductId == SubId).OrderByDescending(i => i.TransactionDateUtc).ToList();
                    var recentSub = sorted[0];
                    if (recentSub != null)
                    {

                        if (recentSub.TransactionDateUtc.AddSubTime() > DateTime.UtcNow)
                        {
                            found = true;
                            GlobalSettings.HasTippedSub = true;
                            GlobalSettings.CheckSubStatus = true;
                            GlobalSettings.SubExpirationDate = recentSub.TransactionDateUtc.AddSubTime();

                            OnPropertyChanged(nameof(IsSubscribed));
                            OnPropertyChanged(nameof(IsNotSubscribed));
                            OnPropertyChanged(nameof(SubStatus));
                        }
                    }
                }

                if (purchases?.Any(p => p.ProductId == SubId6Months) ?? false)
                {
                    var sorted = purchases.Where(p => p.ProductId == SubId6Months).OrderByDescending(i => i.TransactionDateUtc).ToList();
                    var recentSub = sorted[0];
                    if (recentSub != null)
                    {

                        if (recentSub.TransactionDateUtc.AddSubTime(6) > DateTime.UtcNow)
                        {
                            found = true;
                            GlobalSettings.HasTippedSub = true;
                            GlobalSettings.CheckSubStatus = true;
                            GlobalSettings.SubExpirationDate = recentSub.TransactionDateUtc.AddSubTime(6);

                            OnPropertyChanged(nameof(IsSubscribed));
                            OnPropertyChanged(nameof(IsNotSubscribed));
                            OnPropertyChanged(nameof(SubStatus));
                        }
                    }
                }

                if (!found && trycount > 1)
                {
                    trycount = 0;
                    await platformHelpers.DisplayAlert("Hmmmm!", $"Looks like we couldn't find your previous purchases or active subscriptions. Tap on the purchase button to attempt to purchase or restore My Cadence Pro.");
                }
                else if(!found && trycount == 1)
                {
                    IsBusy = false;
                    await CrossInAppBilling.Current.DisconnectAsync();
                    //try again
                    await RestorePurchases();
                    trycount = 0;
                }
                else
                {
                    trycount = 0;
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Issue connecting: " + ex);
                await platformHelpers.DisplayAlert("Uh Oh!", $"Looks like something has gone wrong, please try again or tap on the Purchase button to attempt to restore this specific purchase.  Code: {ex.Message}");
            }
            finally
            {
                await CrossInAppBilling.Current.DisconnectAsync();
                IsBusy = false;
                //IsBuying = false;
                //CellPro.IsEnabled = true;
                //CellRestore.IsEnabled = true;
            }
        }

        public void CancelGoGetPrice()
        {
            try
            {
                if (priceCTS != null && priceToken.CanBeCanceled)
                {
                    priceCTS?.Cancel();
                }
            }
            catch
            {
            }
        }
        public async Task GoGetPrice()
        {
            if (GlobalSettings.IsPro)
                return;

            if (platformHelpers.HasInternet)
            {
                priceCTS = new CancellationTokenSource();
                priceToken = priceCTS.Token;
                var task = Task.Run(GetPrice, priceToken);
                try
                {
                    await task;
                }
                catch
                {
                }
                finally
                {
                    priceCTS?.Dispose();
                }
            }
        }

        CancellationTokenSource priceCTS;
        CancellationToken priceToken;
        async Task GetPrice()
        {
            if (IsBusy)
                return;

            if (IsBronze || IsGold || IsSilver)
                return;


            if (string.IsNullOrWhiteSpace(GlobalSettings.ProPrice) || string.IsNullOrWhiteSpace(GlobalSettings.SubPrice) || GlobalSettings.ProPriceDate.AddDays(7) < DateTime.UtcNow)
            {

            }
            else
            {
#if !DEBUG
                return;
#endif
            }

            // don't do if we dont' have internet
            if (!platformHelpers.HasInternet)
            {
                return;
            }


            IsBusy = true;

            try
            {

/*#if DEBUG
                GlobalSettings.ProPrice = "$2.99";
                OnPropertyChanged(nameof(ProPrice));
                OnPropertyChanged(nameof(SubPrice));
                return;
#endif*/

                //Check Offline

                var connected = await CrossInAppBilling.Current.ConnectAsync();

                if (!connected)
                {
                    return;
                }

                var includeBronze = !platformHelpers.IsMac;
                var ids = platformHelpers.IsMac ? new[] { "mstgold", SubId, SubId6Months } : new[] { "mstbronze", "mstsilver", "mstgold" };
                var items = await CrossInAppBilling.Current.GetProductInfoAsync(ItemType.InAppPurchase, ids);
                if (items == null || items.Count() == 0)
                    return;

                var all = string.Empty;
                foreach(var item in items.OrderBy(s => s.MicrosPrice))
                {
                    if (item.ProductId == "mstbronze" && !includeBronze)
                        continue;

                    if(item.ProductId == SubId)
                    {
                        GlobalSettings.SubPrice = $"{item.LocalizedPrice}";
                        continue;
                    }

                    if (item.ProductId == SubId6Months)
                    {
                        GlobalSettings.SubPrice6Months = $"{item.LocalizedPrice}";
                        continue;
                    }

                    all += $"{item.Name} - {item.LocalizedPrice} | ";
                }

                

                GlobalSettings.ProPrice = all.Replace("My Stream Timer", string.Empty);


                platformHelpers.InvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(ProPrice));
                    OnPropertyChanged(nameof(SubPrice));
                });

                GlobalSettings.ProPriceDate = DateTime.UtcNow;
                
            }
            catch (Exception ex)
            {
                //it is alright that we couldn't get the price
            }
            finally
            {
                await CrossInAppBilling.Current.DisconnectAsync();
                IsBusy = false;
            }
        }
    }
}
