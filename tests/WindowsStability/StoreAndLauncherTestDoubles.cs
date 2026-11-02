// Task-based API doubles for source-linked managed tests, not WinRT/COM/WinUI qualification.
#if WINDOWS_STABILITY_STUBS
namespace Windows.Networking.Connectivity
{
    public enum NetworkConnectivityLevel { None, InternetAccess }
    public static class NetworkInformation
    {
        public static bool Online { get; set; } = true;
        public static Exception? Failure { get; set; }
        public static ConnectionProfile GetInternetConnectionProfile() => Failure is { } failure ? throw failure : new();
    }
    public sealed class ConnectionProfile
    {
        public NetworkConnectivityLevel GetNetworkConnectivityLevel() =>
            NetworkInformation.Online ? NetworkConnectivityLevel.InternetAccess : NetworkConnectivityLevel.None;
    }
}

namespace WinRT.Interop
{
    public static class InitializeWithWindow
    {
        public static void Initialize(object instance, nint handle) { }
    }
}

namespace Windows.Services.Store
{
    public enum StorePurchaseStatus { Succeeded, AlreadyPurchased, NotPurchased, NetworkError, ServerError }
    public sealed class StoreContext
    {
        public static Func<StoreContext> Create { get; set; } = () => new();
        public static StoreContext GetDefault() => Create();
        public event Action<StoreContext, object>? OfflineLicensesChanged;
        public void RaiseOfflineLicensesChanged() => OfflineLicensesChanged?.Invoke(this, new());
        public Func<Task<StoreAppLicense?>> License { get; set; } = () => Task.FromResult<StoreAppLicense?>(new());
        public Func<Task<StoreProductQueryResult>> Products { get; set; } = () => Task.FromResult(new StoreProductQueryResult());
        public Func<Task<StorePurchaseResult>> Purchase { get; set; } = () => Task.FromResult(new StorePurchaseResult());
        public Task<StoreAppLicense?> GetAppLicenseAsync() => License();
        public Task<StoreProductQueryResult> GetAssociatedStoreProductsAsync(string[] kinds) => Products();
        public Task<StorePurchaseResult> RequestPurchaseAsync(string id) => Purchase();
    }
    public sealed class StoreAppLicense
    {
        public Dictionary<string, StoreLicense> AddOnLicenses { get; } = [];
    }
    public sealed class StoreLicense
    {
        public bool IsActive { get; set; }
        public string SkuStoreId { get; set; } = "";
        public string InAppOfferToken { get; set; } = "";
        public DateTimeOffset ExpirationDate { get; set; }
    }
    public sealed class StoreProductQueryResult
    {
        public Exception? ExtendedError { get; set; }
        public Dictionary<string, StoreProduct> Products { get; } = [];
    }
    public sealed class StoreProduct
    {
        public string InAppOfferToken { get; set; } = "";
        public string StoreId { get; set; } = "";
        public string Title { get; set; } = "";
        public StorePrice Price { get; } = new();
        public List<StoreSku> Skus { get; } = [];
    }
    public sealed class StorePrice { public string FormattedPrice { get; set; } = ""; }
    public sealed class StoreSku { public StoreSubscriptionInfo? SubscriptionInfo { get; set; } }
    public sealed class StoreSubscriptionInfo
    {
        public int BillingPeriod { get; set; }
        public StoreDurationUnit BillingPeriodUnit { get; set; } = StoreDurationUnit.Month;
    }
    public enum StoreDurationUnit { Month }
    public sealed class StorePurchaseResult
    {
        public StorePurchaseStatus Status { get; set; }
        public Exception? ExtendedError { get; set; }
    }
    public sealed class StoreSendRequestResult
    {
        public Exception? ExtendedError { get; set; }
        public string Response { get; set; } = "";
    }
    public static class StoreRequestHelper
    {
        public static Func<Task<StoreSendRequestResult>> Send { get; set; } =
            () => Task.FromResult(new StoreSendRequestResult());
        public static Task<StoreSendRequestResult> SendRequestAsync(StoreContext context, uint kind, string data) => Send();
    }
}

namespace Windows.System
{
    public static class Launcher
    {
        public static Func<Task<bool>> Launch { get; set; } = () => Task.FromResult(true);
        public static Task<bool> LaunchUriAsync(Uri uri) => Launch();
        public static Task<bool> LaunchFolderPathAsync(string path) => Launch();
    }
}

namespace Windows.ApplicationModel.DataTransfer
{
    public sealed class DataPackage { public void SetText(string text) { } }
    public static class Clipboard { public static void SetContent(DataPackage package) { } }
}

namespace Microsoft.UI.Xaml.Controls
{
    public enum ContentDialogButton { Close, Primary }
    public enum ContentDialogResult { None, Primary }
    public sealed class TestContent { public object XamlRoot { get; } = new(); }
    public sealed class ContentDialog
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string CloseButtonText { get; set; } = "";
        public string PrimaryButtonText { get; set; } = "";
        public ContentDialogButton DefaultButton { get; set; }
        public object XamlRoot { get; set; } = new();
        public Task<ContentDialogResult> ShowAsync() => throw new NotSupportedException("No native dialogs in these tests.");
    }
}
#endif
