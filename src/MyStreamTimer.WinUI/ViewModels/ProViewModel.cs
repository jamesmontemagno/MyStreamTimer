using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.WinUI.Helpers;
using MyStreamTimer.WinUI.Services;
using Windows.UI;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>One purchase option card.</summary>
public sealed partial class PlanCardViewModel : ObservableObject
{
    public PlanCardViewModel(string id, string title, string description, string glyph, bool isSubscription, IAsyncRelayCommand<PlanCardViewModel?> buyCommand)
    {
        Id = id;
        Title = title;
        Description = description;
        Glyph = glyph;
        IsSubscription = isSubscription;
        BuyCommand = buyCommand;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public string Glyph { get; }

    public bool IsSubscription { get; }

    public IAsyncRelayCommand<PlanCardViewModel?> BuyCommand { get; }

    public string BuyAutomationName => $"Buy {Title}";

    [ObservableProperty]
    public partial string Price { get; set; } = "—";

    [ObservableProperty]
    public partial string BillingPeriod { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotOwned))]
    public partial bool IsOwned { get; set; }

    public bool IsNotOwned => !IsOwned;

    [ObservableProperty]
    public partial bool CanBuy { get; set; } = true;
}

/// <summary>Pro page: entitlement banner, feature list, purchase cards, restore / manage / legal links.</summary>
public sealed partial class ProViewModel : ObservableObject
{
    public const string PrivacyUrl = "https://refractored.com/about/";
    public const string TermsUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
    public const string RestoreNotFoundMessage = "Looks like we couldn't find your previous purchases or active subscriptions. Tap on the purchase button to attempt to purchase or restore.";

    private static readonly Color BronzeColor = ColorHex.Parse("#CD7F32", Colors.Transparent);
    private static readonly Color SilverColor = ColorHex.Parse("#A8A9AD", Colors.Transparent);
    private static readonly Color GoldColor = ColorHex.Parse("#FFD700", Colors.Transparent);

    private readonly GlobalSettings _settings;
    private readonly ProEntitlement _pro;
    private readonly StoreService _store;
    private readonly DialogService _dialogs;
    private readonly LauncherService _launcher;

    public ProViewModel(GlobalSettings settings, ProEntitlement pro, StoreService store, DialogService dialogs, LauncherService launcher)
    {
        _settings = settings;
        _pro = pro;
        _store = store;
        _dialogs = dialogs;
        _launcher = launcher;

        // Purchasable catalogue matches the macOS app: one Lifetime unlock (mstgold) plus the two subscriptions.
        // Legacy Bronze/Silver lifetime purchases are no longer sold but remain valid entitlements (see RefreshStatus).
        Plans =
        [
            new(ProductIds.Gold, "Lifetime", "One-time purchase, yours forever.", "\uE7C1", false, BuyCommand),
            new(ProductIds.SubMonthly, "Monthly", "Billed every month. Cancel any time.", "\uE787", true, BuyCommand),
            new(ProductIds.SubSixMonths, "6 Months", "Best value subscription.", "\uE823", true, BuyCommand),
        ];

        Refresh();
    }

    public ObservableCollection<PlanCardViewModel> Plans { get; }

    public IReadOnlyList<string> Features { get; } =
    [
        "Countdown 4 — a fourth independent countdown",
        "Count Up 2 — a second count-up timer",
        "Current Time — write the clock to a file for your overlay",
        "Auto and Total output formats for every timer",
        "Pop-out timer windows with custom font and colours",
    ];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsPro { get; set; }

    [ObservableProperty]
    public partial string StatusTitle { get; set; } = "Free";

    [ObservableProperty]
    public partial string StatusDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusGlyph { get; set; } = "\uE8A5";

    [ObservableProperty]
    public partial Brush StatusAccentBrush { get; set; } = new SolidColorBrush(Colors.Transparent);

    [ObservableProperty]
    public partial bool HasTierColor { get; set; }

    // ---------- lifecycle ----------

    public void Activate()
    {
        _pro.Changed += OnProChanged;
        _store.ProductsChanged += OnProductsChanged;
        Refresh();
    }

    public void Deactivate()
    {
        _pro.Changed -= OnProChanged;
        _store.ProductsChanged -= OnProductsChanged;
    }

    private void OnProChanged(object? sender, EventArgs e) => App.DispatcherQueue.TryEnqueue(Refresh);

    private void OnProductsChanged(object? sender, EventArgs e) => App.DispatcherQueue.TryEnqueue(RefreshPrices);

    /// <summary>Fetches the latest product catalogue and licenses (fire-and-forget safe).</summary>
    public async Task RefreshFromStoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _store.RefreshProductsAsync();
            if (_settings.HasTippedSub)
            {
                await _store.RefreshLicensesAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProViewModel] Refresh failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        IsPro = _pro.IsPro;
        RefreshStatus();
        RefreshPrices();
    }

    private void RefreshStatus()
    {
        HasTierColor = false;
        StatusAccentBrush = new SolidColorBrush(Colors.Transparent);

        if (_pro.HasLifetime)
        {
            var (tier, color) = _settings.IsGold ? ("Lifetime", GoldColor)
                : _settings.IsSilver ? ("Silver (legacy lifetime)", SilverColor)
                : ("Bronze (legacy lifetime)", BronzeColor);
            StatusTitle = "Pro Lifetime unlocked";
            StatusDescription = $"{tier} · Thank you for supporting My Stream Timer!";
            StatusGlyph = "\uE73E";
            StatusAccentBrush = new SolidColorBrush(color);
            HasTierColor = true;
        }
        else if (_pro.HasActiveSubscription)
        {
            StatusTitle = "Pro subscription active";
            StatusDescription = $"Active until {_settings.SubExpirationDate.ToLocalTime():d}. Thank you for subscribing!";
            StatusGlyph = "\uE73E";
        }
        else if (_settings.HasTippedSub)
        {
            StatusTitle = "Subscription expired";
            StatusDescription = $"Your Pro subscription ended on {_settings.SubExpirationDate.ToLocalTime():d}. Renew below to keep Pro features.";
            StatusGlyph = "\uE7BA";
        }
        else if (_pro.IsPro)
        {
            StatusTitle = "Pro unlocked";
            StatusDescription = "All Pro features are available in this build.";
            StatusGlyph = "\uE73E";
        }
        else
        {
            StatusTitle = "Free";
            StatusDescription = "You're using the free version. Unlock Pro below to get every timer, format and pop-out windows.";
            StatusGlyph = "\uE8A5";
        }
    }

    private void RefreshPrices()
    {
        var cachedLifetime = ParseCachedLifetimePrices(_settings.ProPrice);
        foreach (var plan in Plans)
        {
            var product = _store.Products.FirstOrDefault(p => string.Equals(p.Id, plan.Id, StringComparison.OrdinalIgnoreCase));
            if (product is not null)
            {
                plan.Price = product.FormattedPrice;
                plan.BillingPeriod = product.IsSubscription
                    ? (string.IsNullOrEmpty(product.BillingPeriod) ? "per billing period" : $"every {product.BillingPeriod}")
                    : "one-time purchase";
            }
            else
            {
                plan.Price = plan.Id switch
                {
                    ProductIds.SubMonthly => FirstNonEmpty(_settings.SubPrice, "—"),
                    ProductIds.SubSixMonths => FirstNonEmpty(_settings.SubPrice6Months, "—"),
                    _ => cachedLifetime.TryGetValue("Gold", out var cached) ? cached : "—",
                };
                plan.BillingPeriod = plan.Id switch
                {
                    ProductIds.SubMonthly => "every month",
                    ProductIds.SubSixMonths => "every 6 months",
                    _ => "one-time purchase",
                };
            }

            plan.IsOwned = plan.Id switch
            {
                // Any lifetime tier (incl. legacy Bronze/Silver) satisfies the Lifetime card.
                ProductIds.Gold => _pro.HasLifetime,
                _ => _pro.HasActiveSubscription,
            };
            plan.CanBuy = !IsBusy && !plan.IsOwned && !(plan.IsSubscription && _pro.HasLifetime);
        }
    }

    private static string FirstNonEmpty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>Legacy cache format: "Bronze - $4.99 | Silver - $9.99 | ".</summary>
    private static Dictionary<string, string> ParseCachedLifetimePrices(string cached)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in cached.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = part.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dash <= 0)
            {
                continue;
            }

            var title = part[..dash].Trim();
            var price = part[(dash + 3)..].Trim();
            foreach (var tier in new[] { "Bronze", "Silver", "Gold" })
            {
                if (title.Contains(tier, StringComparison.OrdinalIgnoreCase))
                {
                    result[tier] = price;
                }
            }
        }

        return result;
    }

    // ---------- commands ----------

    [RelayCommand]
    private async Task BuyAsync(PlanCardViewModel? plan)
    {
        if (plan is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        RefreshPrices();
        try
        {
            var (success, error) = await _store.PurchaseAsync(plan.Id);
            if (!success && error is not null)
            {
                await _dialogs.ShowMessageAsync("Uh Oh!", error);
            }
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RefreshPrices();
        try
        {
            if (!StoreService.HasInternet)
            {
                await _dialogs.ShowMessageAsync("Uh Oh!", StoreService.NetworkErrorMessage);
                return;
            }

            var found = await _store.RestoreAsync();
            if (!found)
            {
                await _dialogs.ShowMessageAsync("Restore purchases", RestoreNotFoundMessage);
            }
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand]
    private Task ManageSubscriptionAsync() => _launcher.OpenUriAsync(StoreService.ManageSubscriptionUrl);

    [RelayCommand]
    private Task OpenPrivacyAsync() => _launcher.OpenUriAsync(PrivacyUrl);

    [RelayCommand]
    private Task OpenTermsAsync() => _launcher.OpenUriAsync(TermsUrl);
}
