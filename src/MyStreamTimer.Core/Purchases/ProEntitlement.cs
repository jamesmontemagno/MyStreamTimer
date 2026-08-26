using MyStreamTimer.Core.Settings;

namespace MyStreamTimer.Core.Purchases;

public static class ProductIds
{
    public const string Bronze = "mstbronze";
    public const string Silver = "mstsilver";
    public const string Gold = "mstgold";
    public const string SubMonthly = "mstsub";
    public const string SubSixMonths = "mstsub6months";

    public static readonly IReadOnlyList<string> Lifetime = [Bronze, Silver, Gold];
    public static readonly IReadOnlyList<string> Subscriptions = [SubMonthly, SubSixMonths];
    public static readonly IReadOnlyList<string> All = [Bronze, Silver, Gold, SubMonthly, SubSixMonths];

    /// <summary>What the app sells today (matches macOS): one Lifetime unlock + two subscriptions. Bronze/Silver remain valid legacy entitlements.</summary>
    public static readonly IReadOnlyList<string> Purchasable = [Gold, SubMonthly, SubSixMonths];

    public static bool IsSubscription(string id) => id is SubMonthly or SubSixMonths;
}

/// <summary>
/// Pro entitlement rules — identical to legacy <c>GlobalSettings.IsPro</c>:
/// any lifetime tier, or a subscription whose expiry is in the future. DEBUG builds are always Pro.
/// </summary>
public sealed class ProEntitlement
{
    readonly GlobalSettings settings;

    public ProEntitlement(GlobalSettings settings) => this.settings = settings;

    public event EventHandler? Changed;

    public bool IsPro
    {
        get
        {
#if DEBUG
            if (ForceProInDebug)
                return true;
#endif
            return settings.IsBronze || settings.IsSilver || settings.IsGold || (settings.HasTippedSub && settings.IsSubValid);
        }
    }

    /// <summary>Debug-only switch so the free experience can still be exercised in Debug builds.</summary>
    public bool ForceProInDebug { get; set; } = true;

    public bool HasLifetime => settings.IsBronze || settings.IsSilver || settings.IsGold;
    public bool HasActiveSubscription => settings.HasTippedSub && settings.IsSubValid;

    /// <summary>Legacy grace: a subscription purchase is considered valid for N months + 5 days.</summary>
    public static DateTime AddSubTime(DateTime purchasedUtc, int months = 1) => purchasedUtc.AddMonths(months).AddDays(5);

    public void ApplyLifetime(string productId)
    {
        switch (productId)
        {
            case ProductIds.Bronze: settings.IsBronze = true; break;
            case ProductIds.Silver: settings.IsSilver = true; break;
            case ProductIds.Gold: settings.IsGold = true; break;
            default: return;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySubscription(DateTime expirationUtc)
    {
        settings.HasTippedSub = true;
        settings.CheckSubStatus = true;
        settings.SubExpirationDate = expirationUtc;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called with the authoritative license set from the Store. Never clears lifetime flags (a lifetime
    /// unlock is permanent even if the Store is temporarily unreachable); subscription flags are refreshed.
    /// </summary>
    public void Sync(IEnumerable<string> activeLifetimeIds, DateTime? subscriptionExpirationUtc)
    {
        foreach (var id in activeLifetimeIds)
            ApplyLifetime(id);

        if (subscriptionExpirationUtc is { } exp)
            ApplySubscription(exp);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

