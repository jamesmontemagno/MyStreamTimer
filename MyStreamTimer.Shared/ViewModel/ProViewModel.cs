using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MvvmHelpers;
using MvvmHelpers.Commands;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using Plugin.InAppBilling;

namespace MyStreamTimer.Shared.ViewModel
{
    public class ProViewModel : BaseViewModel
    {

        public AsyncCommand<string> BuyCommand { get; }
        public AsyncCommand RestoreCommand { get; }
        IPlatformHelpers platformHelpers;
        public ProViewModel()
        {
            platformHelpers = ServiceContainer.Resolve<IPlatformHelpers>();
            BuyCommand = new AsyncCommand<string>(PurchasePro);
            RestoreCommand = new AsyncCommand(RestorePurchases);

#if DEBUG
            CrossInAppBilling.Current.InTestingMode = true;
#endif
        }

        public string ProPrice
        {
            get
            {
                if (string.IsNullOrWhiteSpace(GlobalSettings.ProPrice))
                    return "PRO MODES";

                return $"PRO MODES - {GlobalSettings.ProPrice}";
            }
        }

        public bool IsBronze => GlobalSettings.IsBronze;
        public bool IsNotBronze => !IsBronze;

        public bool IsSilver => GlobalSettings.IsSilver;
        public bool IsNotSilver => !IsSilver;

        public bool IsGold => GlobalSettings.IsGold;
        public bool IsNotGold => !IsGold;

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
                var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase);

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

                if (!found)
                {
                    await platformHelpers.DisplayAlert("Hmmmm!", $"Looks like we couldn't find your previous purchase. Tap on the purchase button to attempt to purchase or restore My Cadence Pro.");
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
            if (IsBronze || IsGold || IsSilver)
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


            if (string.IsNullOrWhiteSpace(GlobalSettings.ProPrice) || GlobalSettings.ProPriceDate.AddDays(7) < DateTime.UtcNow)
            {

            }
            else
            {
                return;
            }

            // don't do if we dont' have internet
            if (!platformHelpers.HasInternet)
            {
                return;
            }


            IsBusy = true;

            try
            {

#if DEBUG
                GlobalSettings.ProPrice = "$2.99";
                OnPropertyChanged(nameof(ProPrice));
                return;
#endif

                //Check Offline

                var connected = await CrossInAppBilling.Current.ConnectAsync();

                if (!connected)
                {
                    return;
                }

                var items = await CrossInAppBilling.Current.GetProductInfoAsync(ItemType.InAppPurchase, "mstbronze", "mstsilver", "mstgold");
                if (items == null || items.Count() == 0)
                    return;

                var all = string.Empty;
                foreach(var item in items.OrderBy(s => s.MicrosPrice))
                {
                    all += $"{item.Name} - {item.LocalizedPrice} | ";
                }

                

                GlobalSettings.ProPrice = all.Replace("My Stream Timer", string.Empty);
                platformHelpers.InvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(ProPrice));
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
