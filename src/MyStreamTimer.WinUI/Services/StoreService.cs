using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using Windows.Networking.Connectivity;
using Windows.Services.Store;

namespace MyStreamTimer.WinUI.Services;

/// <summary>Cached, UI-friendly view of a Store add-on.</summary>
public sealed record StoreProductInfo(string Id, string StoreId, string Title, string FormattedPrice, bool IsSubscription, string BillingPeriod);

/// <summary>
/// Wraps <see cref="StoreContext"/>: product catalogue, licenses → <see cref="ProEntitlement"/>, purchases
/// (durables and subscriptions), restore, and the rating prompt. Store API failures are recorded and
/// mapped to operation outcomes; cached entitlements are not cleared on lookup failure.
/// </summary>
public sealed class StoreService
{
    public const string NetworkErrorMessage = "Unable to connect to the app store, check your internet connectivity and try again.";
    public const string ServerErrorMessage = "Something has gone wrong, please try again.";
    public const string ManageSubscriptionUrl = "https://account.microsoft.com/services";

    private readonly GlobalSettings _settings;
    private readonly ProEntitlement _pro;
    private StoreContext? _context;
    private List<StoreProductInfo> _products = [];
    private readonly Dictionary<string, StoreProduct> _storeProducts = new(StringComparer.OrdinalIgnoreCase);

    public StoreService(GlobalSettings settings, ProEntitlement pro)
    {
        _settings = settings;
        _pro = pro;
    }

    /// <summary>Products fetched by the last successful <see cref="RefreshProductsAsync"/>.</summary>
    public IReadOnlyList<StoreProductInfo> Products => _products;

    /// <summary>Raised (on the calling thread) after the product list changes.</summary>
    public event EventHandler? ProductsChanged;

    public static bool HasInternet
    {
        get
        {
            try
            {
                return NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            }
            catch (Exception ex)
            {
                AppDiagnostics.Record("store-connectivity-failed", "Could not determine Store connectivity.", ex);
                return false;
            }
        }
    }

    private StoreContext? Context
    {
        get
        {
            if (_context is not null)
            {
                return _context;
            }

            try
            {
                _context = StoreContext.GetDefault();
                WinRT.Interop.InitializeWithWindow.Initialize(_context, App.WindowHandle);
                _context.OfflineLicensesChanged += OnOfflineLicensesChanged;
            }
            catch (Exception ex)
            {
                AppDiagnostics.Record("store-context-unavailable", "Could not initialize the Store context.", ex);
                _context = null;
            }

            return _context;
        }
    }

    private void OnOfflineLicensesChanged(StoreContext sender, object args)
    {
        App.UiDispatcher.TryEnqueue(async () => await RefreshLicensesAsync());
    }

    /// <summary>Loads durable + subscription add-ons and caches legacy price strings in settings.</summary>
    public async Task RefreshProductsAsync()
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        try
        {
            var result = await context.GetAssociatedStoreProductsAsync(["Durable"]); // subscription add-ons are reported as Durable (detected via SubscriptionInfo)
            if (result.ExtendedError is not null)
            {
                AppDiagnostics.Record("store-products-failed", "The Store product lookup failed.", result.ExtendedError);
                return;
            }

            var list = new List<StoreProductInfo>();
            _storeProducts.Clear();
            foreach (var product in result.Products.Values)
            {
                var id = ProductIds.Purchasable.FirstOrDefault(p => string.Equals(p, product.InAppOfferToken, StringComparison.OrdinalIgnoreCase));
                if (id is null)
                {
                    continue;
                }

                _storeProducts[id] = product;
                var isSub = ProductIds.IsSubscription(id);
                var period = string.Empty;
                var sku = product.Skus.FirstOrDefault();
                if (isSub && sku?.SubscriptionInfo is { } info)
                {
                    var unit = info.BillingPeriodUnit.ToString().ToLowerInvariant();
                    period = info.BillingPeriod == 1 ? $"1 {unit}" : $"{info.BillingPeriod} {unit}s";
                }

                list.Add(new StoreProductInfo(id, product.StoreId, CleanTitle(product.Title), product.Price.FormattedPrice, isSub, period));
            }

            _products = list;
            WritePriceSettings(list);
            ProductsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("store-products-failed", "The Store product lookup failed.", ex);
        }
    }

    private static string CleanTitle(string title) =>
        title.Replace("My Stream Timer", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(' ', '-', ':');

    private void WritePriceSettings(IReadOnlyList<StoreProductInfo> products)
    {
        var pro = string.Empty;
        foreach (var id in ProductIds.Lifetime)
        {
            var p = products.FirstOrDefault(x => x.Id == id);
            if (p is not null)
            {
                pro += $"{p.Title} - {p.FormattedPrice} | ";
            }
        }

        if (pro.Length > 0)
        {
            _settings.ProPrice = pro;
        }

        var monthly = products.FirstOrDefault(x => x.Id == ProductIds.SubMonthly);
        if (monthly is not null)
        {
            _settings.SubPrice = monthly.FormattedPrice;
        }

        var six = products.FirstOrDefault(x => x.Id == ProductIds.SubSixMonths);
        if (six is not null)
        {
            _settings.SubPrice6Months = six.FormattedPrice;
        }

        _settings.ProPriceDate = DateTime.UtcNow;
    }

    /// <summary>Applies recognized active licenses additively, distinguishing empty lookup results from unavailable or failed lookups.</summary>
    public async Task<StoreLicenseRefreshOutcome> RefreshLicensesAsync()
    {
        var context = Context;
        if (context is null)
        {
            return StoreLicenseRefreshOutcome.Unavailable;
        }

        try
        {
            var license = await context.GetAppLicenseAsync();
            if (license is null)
            {
                AppDiagnostics.Record("store-licenses-failed", "The Store returned no app license.");
                return StoreLicenseRefreshOutcome.Failed;
            }

            var lifetime = new List<string>();
            DateTime? subExpiration = null;

            foreach (var addOn in license.AddOnLicenses.Values)
            {
                if (!addOn.IsActive)
                {
                    continue;
                }

                var productId = MapSkuToProductId(addOn.SkuStoreId, addOn.InAppOfferToken);
                if (productId is null)
                {
                    continue;
                }

                if (ProductIds.IsSubscription(productId))
                {
                    var exp = addOn.ExpirationDate.UtcDateTime;
                    if (subExpiration is null || exp > subExpiration)
                    {
                        subExpiration = exp;
                    }
                }
                else
                {
                    lifetime.Add(productId);
                }
            }

            if (lifetime.Count == 0 && subExpiration is null)
            {
                return StoreLicenseRefreshOutcome.NoActiveEntitlements;
            }

            _pro.Sync(lifetime, subExpiration);
            return StoreLicenseRefreshOutcome.ActiveEntitlements;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("store-licenses-failed", "The Store license refresh failed.", ex);
            return StoreLicenseRefreshOutcome.Failed;
        }
    }

    private string? MapSkuToProductId(string skuStoreId, string? inAppOfferToken)
    {
        if (!string.IsNullOrEmpty(inAppOfferToken))
        {
            var byToken = ProductIds.All.FirstOrDefault(p => string.Equals(p, inAppOfferToken, StringComparison.OrdinalIgnoreCase));
            if (byToken is not null)
            {
                return byToken;
            }
        }

        var prefix = skuStoreId.Split('/')[0];
        foreach (var (id, product) in _storeProducts)
        {
            if (string.Equals(product.StoreId, prefix, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return ProductIds.All.FirstOrDefault(p => prefix.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Purchases an add-on by product id. Returns (false, null) when the user cancelled.</summary>
    public async Task<(bool Success, string? Error)> PurchaseAsync(string productId)
    {
        if (!HasInternet)
        {
            AppDiagnostics.Record("store-purchase-offline", "The purchase was not started because Internet access is unavailable.");
            return (false, NetworkErrorMessage);
        }

        var context = Context;
        if (context is null)
        {
            return (false, ServerErrorMessage);
        }

        try
        {
            if (!_storeProducts.TryGetValue(productId, out var product))
            {
                await RefreshProductsAsync();
                if (!_storeProducts.TryGetValue(productId, out product))
                {
                    AppDiagnostics.Record("store-purchase-unavailable", "The purchase could not start because its product is unavailable.");
                    return (false, ServerErrorMessage);
                }
            }

            var result = await context.RequestPurchaseAsync(product.StoreId);
            switch (result.Status)
            {
                case StorePurchaseStatus.Succeeded:
                case StorePurchaseStatus.AlreadyPurchased:
                    await RefreshLicensesAsync();
                    return (true, null);
                case StorePurchaseStatus.NotPurchased:
                    return (false, null);
                case StorePurchaseStatus.NetworkError:
                    AppDiagnostics.Record("store-purchase-network-error", "The Store reported a purchase network error.", result.ExtendedError);
                    return (false, NetworkErrorMessage);
                default:
                    AppDiagnostics.Record("store-purchase-failed", "The Store purchase failed.", result.ExtendedError);
                    return (false, ServerErrorMessage);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("store-purchase-failed", "The Store purchase failed.", ex);
            return (false, ServerErrorMessage);
        }
    }

    /// <summary>Re-reads licenses without treating lookup failure as an empty purchase history.</summary>
    public Task<StoreLicenseRefreshOutcome> RestoreAsync() => RefreshLicensesAsync();

    /// <summary>Shows the Store rating/review dialog (request 16).</summary>
    public async Task RequestRatingAsync()
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        try
        {
            var result = await StoreRequestHelper.SendRequestAsync(context, 16, string.Empty);
            if (result.ExtendedError is not null)
            {
                AppDiagnostics.Record("store-rating-failed", "The Store rating request failed.", result.ExtendedError);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Record("store-rating-failed", "The Store rating request failed.", ex);
        }
    }
}
