using System.IO.Compression;
using MyStreamTimer.Core.Diagnostics;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.WinUI.Services;
using Windows.Networking.Connectivity;
using Windows.Services.Store;
using Windows.System;

namespace MyStreamTimer.WindowsStability.Tests;

[Collection("Windows stability")]
public sealed class StoreAndLauncherTests
{
    const string Secret = "PRIVATE-account-license-folder-uri";
    readonly InMemorySettingsStore values = new();
    readonly GlobalSettings settings;
    readonly StoreContext context = new();
    readonly StoreService service;

    public StoreAndLauncherTests()
    {
        AppDiagnostics.Records.Clear();
        NetworkInformation.Online = true;
        NetworkInformation.Failure = null;
        StoreContext.Create = () => context;
        StoreRequestHelper.Send = () => Task.FromResult(new StoreSendRequestResult());
        Launcher.Launch = () => Task.FromResult(true);
        settings = new(values, "unused-fixture-path");
        settings.IsBronze = true;
        settings.HasTippedSub = true;
        settings.SubExpirationDate = DateTime.UtcNow.AddDays(20);
        service = new(settings, new ProEntitlement(settings) { ForceProInDebug = false });
    }

    [Theory]
    [InlineData("fault")]
    [InlineData("null")]
    [InlineData("unavailable")]
    public async Task Failed_or_unavailable_lookup_is_not_reported_as_successful_empty_lookup(string outcome)
    {
        var before = values.Values.ToArray();
        var empty = await service.RestoreAsync();
        var otherService = service;
        if (outcome == "unavailable")
        {
            StoreContext.Create = () => throw Failure();
            otherService = new(settings, new ProEntitlement(settings));
        }
        else
        {
            context.License = outcome == "null"
                ? () => Task.FromResult<StoreAppLicense?>(null)
                : () => Task.FromException<StoreAppLicense?>(Failure());
        }

        Assert.NotEqual(empty, await otherService.RestoreAsync());
        Assert.Equal(before, values.Values.ToArray());
    }

    [Theory]
    [InlineData("empty", StoreLicenseRefreshOutcome.NoActiveEntitlements)]
    [InlineData("inactive", StoreLicenseRefreshOutcome.NoActiveEntitlements)]
    [InlineData("unknown", StoreLicenseRefreshOutcome.NoActiveEntitlements)]
    [InlineData("null", StoreLicenseRefreshOutcome.Failed)]
    [InlineData("fault", StoreLicenseRefreshOutcome.Failed)]
    [InlineData("unavailable", StoreLicenseRefreshOutcome.Unavailable)]
    public async Task Non_active_outcomes_preserve_all_cached_settings(string kind, StoreLicenseRefreshOutcome expected)
    {
        var before = values.Values.ToArray();
        var license = new StoreAppLicense();
        if (kind is "inactive" or "unknown")
            license.AddOnLicenses.Add(Secret, new StoreLicense
            {
                IsActive = kind == "unknown",
                InAppOfferToken = kind == "unknown" ? Secret : ProductIds.Gold,
            });
        context.License = kind switch
        {
            "fault" => () => Task.FromException<StoreAppLicense?>(Failure()),
            "null" => () => Task.FromResult<StoreAppLicense?>(null),
            _ => () => Task.FromResult<StoreAppLicense?>(license),
        };
        if (kind == "unavailable")
            StoreContext.Create = () => throw Failure();

        Assert.Equal(expected, await service.RestoreAsync());
        Assert.Equal(before, values.Values.ToArray());
        if (kind == "null")
        {
            var record = Assert.Single(AppDiagnostics.Records);
            Assert.Equal("store-licenses-failed", record.Event);
            Assert.Null(record.Exception);
        }
    }

    [Fact]
    public async Task Recognized_active_licenses_sync_additively_using_latest_subscription_expiration()
    {
        var expiration = DateTime.UtcNow.AddDays(60);
        var license = new StoreAppLicense();
        license.AddOnLicenses.Add("gold", new StoreLicense { IsActive = true, InAppOfferToken = ProductIds.Gold });
        license.AddOnLicenses.Add("sub", new StoreLicense
        {
            IsActive = true, InAppOfferToken = ProductIds.SubMonthly, ExpirationDate = expiration.AddDays(-10),
        });
        license.AddOnLicenses.Add("sub6", new StoreLicense
        {
            IsActive = true, InAppOfferToken = ProductIds.SubSixMonths, ExpirationDate = expiration,
        });
        var pending = new TaskCompletionSource<StoreAppLicense?>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.License = () => pending.Task;
        var before = values.Values.ToArray();
        var refresh = service.RestoreAsync();
        Assert.False(refresh.IsCompleted);
        Assert.Equal(before, values.Values.ToArray());
        pending.SetResult(license);

        Assert.Equal(StoreLicenseRefreshOutcome.ActiveEntitlements, await refresh);
        Assert.True(settings.IsBronze);
        Assert.True(settings.IsGold);
        Assert.True(settings.HasTippedSub);
        Assert.True(settings.CheckSubStatus);
        Assert.Equal(expiration, settings.SubExpirationDate);
        Assert.Empty(AppDiagnostics.Records);
    }

    [Theory]
    [InlineData(StoreLicenseRefreshOutcome.ActiveEntitlements, null)]
    [InlineData(StoreLicenseRefreshOutcome.NoActiveEntitlements, "No active purchases or subscriptions were found for this app. Your cached access has not been cleared.")]
    [InlineData(StoreLicenseRefreshOutcome.Unavailable, "The Microsoft Store is unavailable. Try restoring purchases again later. Your cached access has not been cleared.")]
    [InlineData(StoreLicenseRefreshOutcome.Failed, "Couldn't check your purchases with the Microsoft Store. Please try again. Your cached access has not been cleared.")]
    public void Restore_UI_mapping_distinguishes_all_outcomes(StoreLicenseRefreshOutcome outcome, string? expected)
    {
        Assert.Equal(expected, outcome.GetRestoreMessage());
    }

    [Fact]
    public void Unknown_outcome_does_not_silently_look_successful() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((StoreLicenseRefreshOutcome)99).GetRestoreMessage());

    [Theory]
    [InlineData(StorePurchaseStatus.Succeeded, "fault")]
    [InlineData(StorePurchaseStatus.AlreadyPurchased, "fault")]
    [InlineData(StorePurchaseStatus.Succeeded, "null")]
    [InlineData(StorePurchaseStatus.AlreadyPurchased, "null")]
    [InlineData(StorePurchaseStatus.Succeeded, "empty")]
    [InlineData(StorePurchaseStatus.AlreadyPurchased, "empty")]
    public async Task Completed_transaction_stays_successful_even_without_a_refreshed_entitlement(StorePurchaseStatus status, string refresh)
    {
        SetProduct();
        await service.RefreshProductsAsync();
        var before = values.Values.ToArray();
        context.Purchase = () => Task.FromResult(new StorePurchaseResult { Status = status });
        context.License = refresh switch
        {
            "fault" => () => Task.FromException<StoreAppLicense?>(Failure()),
            "null" => () => Task.FromResult<StoreAppLicense?>(null),
            _ => () => Task.FromResult<StoreAppLicense?>(new()),
        };
        Assert.Equal((true, (string?)null), await service.PurchaseAsync(ProductIds.Gold));
        Assert.Equal(before, values.Values.ToArray());
        if (refresh != "empty")
            Assert.Equal("store-licenses-failed", Assert.Single(AppDiagnostics.Records).Event);
    }

    [Theory]
    [InlineData(StorePurchaseStatus.NotPurchased, null, null)]
    [InlineData(StorePurchaseStatus.NetworkError, StoreService.NetworkErrorMessage, "store-purchase-network-error")]
    [InlineData(StorePurchaseStatus.ServerError, StoreService.ServerErrorMessage, "store-purchase-failed")]
    public async Task Uncompleted_purchase_keeps_cancellation_and_error_semantics(StorePurchaseStatus status, string? error, string? category)
    {
        SetProduct();
        await service.RefreshProductsAsync();
        var before = values.Values.ToArray();
        var lookups = 0;
        context.License = () => { lookups++; return Task.FromResult<StoreAppLicense?>(new()); };
        context.Purchase = () => Task.FromResult(new StorePurchaseResult { Status = status });

        Assert.Equal((false, error), await service.PurchaseAsync(ProductIds.Gold));
        Assert.Equal(0, lookups);
        Assert.Equal(before, values.Values.ToArray());
        if (category is null)
            Assert.Empty(AppDiagnostics.Records);
        else
            Assert.Equal(category, Assert.Single(AppDiagnostics.Records).Event);
    }

    [Theory]
    [InlineData("offline", StoreService.NetworkErrorMessage, "store-purchase-offline")]
    [InlineData("unavailable", StoreService.ServerErrorMessage, "store-context-unavailable")]
    [InlineData("missing-product", StoreService.ServerErrorMessage, "store-purchase-unavailable")]
    public async Task Purchase_preconditions_return_existing_errors_without_mutating_entitlements(string kind, string error, string category)
    {
        var before = values.Values.ToArray();
        var purchases = 0;
        context.Purchase = () => { purchases++; return Task.FromResult(new StorePurchaseResult()); };
        if (kind == "offline")
            NetworkInformation.Online = false;
        if (kind == "unavailable")
            StoreContext.Create = () => throw Failure();
        var expiration = settings.SubExpirationDate;
        Assert.Equal((false, error), await service.PurchaseAsync(ProductIds.Gold));
        Assert.Equal(0, purchases);
        Assert.Equal(category, Assert.Single(AppDiagnostics.Records).Event);
        Assert.True(settings.IsBronze);
        Assert.True(settings.HasTippedSub);
        Assert.Equal(expiration, settings.SubExpirationDate);
        if (kind != "missing-product")
            Assert.Equal(before, values.Values.ToArray());
    }

    [Theory]
    [InlineData(StorePurchaseStatus.Succeeded)]
    [InlineData(StorePurchaseStatus.AlreadyPurchased)]
    public async Task Successful_purchase_applies_recognized_licenses(StorePurchaseStatus status)
    {
        SetProduct();
        var license = new StoreAppLicense();
        license.AddOnLicenses.Add(Secret, new StoreLicense { IsActive = true, InAppOfferToken = ProductIds.Gold });
        context.License = () => Task.FromResult<StoreAppLicense?>(license);
        context.Purchase = () => Task.FromResult(new StorePurchaseResult { Status = status });

        Assert.Equal((true, (string?)null), await service.PurchaseAsync(ProductIds.Gold));
        Assert.True(settings.IsGold);
        Assert.True(settings.IsBronze);
        Assert.Empty(AppDiagnostics.Records);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_product_refresh_preserves_existing_catalogue_and_price_cache(bool throws)
    {
        SetProduct();
        await service.RefreshProductsAsync();
        var products = service.Products;
        var before = values.Values.ToArray();
        context.Products = throws
            ? () => Task.FromException<StoreProductQueryResult>(Failure())
            : () => Task.FromResult(new StoreProductQueryResult { ExtendedError = Failure() });
        await service.RefreshProductsAsync();
        Assert.Same(products, service.Products);
        Assert.Equal(before, values.Values.ToArray());
        Assert.Equal("store-products-failed", Assert.Single(AppDiagnostics.Records).Event);
    }

    [Fact]
    public async Task Successful_rating_does_not_log_its_response_payload()
    {
        StoreRequestHelper.Send = () => Task.FromResult(new StoreSendRequestResult { Response = Secret });
        await service.RequestRatingAsync();
        Assert.Empty(AppDiagnostics.Records);
    }

    [Theory]
    [InlineData("connectivity", "store-connectivity-failed")]
    [InlineData("context", "store-context-unavailable")]
    [InlineData("products-error", "store-products-failed")]
    [InlineData("products-throw", "store-products-failed")]
    [InlineData("license", "store-licenses-failed")]
    [InlineData("purchase-error", "store-purchase-failed")]
    [InlineData("purchase-throw", "store-purchase-failed")]
    [InlineData("rating-error", "store-rating-failed")]
    [InlineData("rating-throw", "store-rating-failed")]
    [InlineData("uri", "launcher-uri-failed")]
    [InlineData("folder", "launcher-folder-failed")]
    public async Task Failures_record_exportable_metadata_without_private_payloads(string boundary, string category)
    {
        var failure = Failure();
        switch (boundary)
        {
            case "connectivity":
                NetworkInformation.Failure = failure;
                Assert.False(StoreService.HasInternet);
                break;
            case "context":
                StoreContext.Create = () => throw failure;
                await service.RefreshProductsAsync();
                break;
            case "products-error":
                context.Products = () => Task.FromResult(new StoreProductQueryResult { ExtendedError = failure });
                await service.RefreshProductsAsync();
                break;
            case "products-throw":
                context.Products = () => Task.FromException<StoreProductQueryResult>(failure);
                await service.RefreshProductsAsync();
                break;
            case "license":
                context.License = () => Task.FromException<StoreAppLicense?>(failure);
                await service.RestoreAsync();
                break;
            case "purchase-error":
            case "purchase-throw":
                SetProduct();
                context.Purchase = boundary == "purchase-error"
                    ? () => Task.FromResult(new StorePurchaseResult { Status = StorePurchaseStatus.ServerError, ExtendedError = failure })
                    : () => Task.FromException<StorePurchaseResult>(failure);
                Assert.Equal((false, StoreService.ServerErrorMessage), await service.PurchaseAsync(ProductIds.Gold));
                break;
            case "rating-error":
            case "rating-throw":
                StoreRequestHelper.Send = boundary == "rating-error"
                    ? () => Task.FromResult(new StoreSendRequestResult { ExtendedError = failure, Response = Secret })
                    : () => Task.FromException<StoreSendRequestResult>(failure);
                await service.RequestRatingAsync();
                break;
            default:
                Launcher.Launch = () => Task.FromException<bool>(failure);
                Assert.False(await Launch(boundary));
                break;
        }

        var record = Assert.Single(AppDiagnostics.Records);
        Assert.Equal(category, record.Event);
        Assert.Same(failure, record.Exception);
        var path = Path.Combine(Path.GetTempPath(), "mst-boundary-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new DiagnosticLog(path);
            log.Write(record.Event, record.Details, record.Exception);
            var zip = Path.Combine(path, "report.zip");
            Assert.True(log.TryExport(zip, out var error));
            Assert.Null(error);
            using var archive = ZipFile.OpenRead(zip);
            using var reader = new StreamReader(Assert.Single(archive.Entries).Open());
            var text = reader.ReadToEnd();
            Assert.Contains(category, text);
            Assert.Contains("System.InvalidOperationException", text);
            Assert.DoesNotContain("PRIVATE", text);
            Assert.DoesNotContain("unused-fixture-path", text);
            Assert.DoesNotContain(".cs:", text);
            Assert.DoesNotContain("/root/", text);
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    [Theory]
    [InlineData("uri", false)]
    [InlineData("folder", false)]
    [InlineData("uri", true)]
    [InlineData("folder", true)]
    public async Task Launcher_preserves_native_bool_and_does_not_invent_an_exception(string kind, bool result)
    {
        Launcher.Launch = () => Task.FromResult(result);
        Assert.Equal(result, await Launch(kind));
        if (result)
            Assert.Empty(AppDiagnostics.Records);
        else
        {
            var record = Assert.Single(AppDiagnostics.Records);
            Assert.Equal($"launcher-{kind}-not-opened", record.Event);
            Assert.Null(record.Exception);
        }
    }

    static Task<bool> Launch(string kind) => kind == "uri"
        ? new LauncherService().OpenUriAsync("https://example.invalid/" + Secret)
        : new LauncherService().OpenFolderAsync(Secret);

    void SetProduct()
    {
        var products = new StoreProductQueryResult();
        products.Products.Add(Secret, new StoreProduct { InAppOfferToken = ProductIds.Gold, StoreId = Secret });
        context.Products = () => Task.FromResult(products);
    }

    static Exception Failure()
    {
        try
        {
            throw new InvalidOperationException(Secret, new IOException(Secret)) { Source = Secret };
        }
        catch (InvalidOperationException ex)
        {
            ex.Data[Secret] = Secret;
            return ex;
        }
    }
}
