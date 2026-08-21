using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Settings;

public class GlobalSettingsRoundTripTests
{
    [Fact]
    public void Every_global_property_round_trips_with_legacy_key_names()
    {
        var store = new InMemorySettingsStore();
        var g = new GlobalSettings(store, @"C:\d");

        g.DirectoryPath = @"D:\obs"; Assert.Equal(@"D:\obs", store.GetString("global_directory_path", ""));
        g.TimesUsed = 7; Assert.Equal(7, store.GetInt("TimesUsed", 0));
        g.IsBronze = true; Assert.True(store.GetBool("IsBronze", false));
        g.IsSilver = true; Assert.True(store.GetBool("IsSilver", false));
        g.IsGold = true; Assert.True(store.GetBool("IsGold", false));
        g.CheckSubStatus = false; Assert.False(g.CheckSubStatus);
        g.SubPrice = "$1.99"; Assert.Equal("$1.99", g.SubPrice);
        g.SubPrice6Months = "$9.99"; Assert.Equal("$9.99", g.SubPrice6Months);
        g.HasTippedSub = true; Assert.True(g.HasTippedSub);
        g.ShowSupportPopUp = false; Assert.False(g.ShowSupportPopUp);
        var exp = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        g.SubExpirationDate = exp; Assert.Equal(exp, g.SubExpirationDate); Assert.True(g.IsSubValid);
        g.ProPrice = "Bronze - $2.99 | "; Assert.Equal("Bronze - $2.99 | ", g.ProPrice);
        var pd = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        g.ProPriceDate = pd; Assert.Equal(pd, g.ProPriceDate);
        g.StayOnTop = false; Assert.False(g.StayOnTop);
        g.FirstRun = false; Assert.False(g.FirstRun);

        // 3.0 keys
        Assert.Equal("system", g.AppTheme); g.AppTheme = "dark"; Assert.Equal("dark", g.AppTheme);
        Assert.Equal(48, g.PopOutFontSize); g.PopOutFontSize = 72; Assert.Equal(72, g.PopOutFontSize);
        g.PopOutFontFamily = "Cascadia Mono"; Assert.Equal("Cascadia Mono", g.PopOutFontFamily);
        Assert.Equal("#FFFFFF", g.PopOutTextColorHex); g.PopOutTextColorHex = "#00FF00"; Assert.Equal("#00FF00", g.PopOutTextColorHex);
        Assert.Equal("#000000", g.PopOutBackgroundColorHex); g.PopOutBackgroundColorHex = "#123456"; Assert.Equal("#123456", g.PopOutBackgroundColorHex);
        Assert.False(g.HasSeenWelcomeBackV1); g.HasSeenWelcomeBackV1 = true; Assert.True(g.HasSeenWelcomeBackV1);
        g.LastSelectedPage = "countdown2"; Assert.Equal("countdown2", g.LastSelectedPage);
        g.MainWindowBounds = "1,2,3,4"; Assert.Equal("1,2,3,4", g.MainWindowBounds);
        Assert.Equal(@"C:\d", g.DefaultDirectoryPath);
    }

    [Fact]
    public void Timer_settings_3_0_properties_round_trip()
    {
        var store = new InMemorySettingsStore();
        var s = new TimerSettings(store, TimerKind.Countdown2);
        Assert.Equal("Countdown 2", s.EffectiveTitle);
        Assert.Equal(TimerKind.Countdown2.DefaultIconGlyph(), s.EffectiveIconGlyph);

        s.DisplayName = "  Giveaway  ";
        s.IconGlyph = "\uE7C8";
        s.PopOutBounds = "10,20,400,160";
        Assert.Equal("Giveaway", s.EffectiveTitle);
        Assert.Equal("\uE7C8", s.EffectiveIconGlyph);
        Assert.Equal("  Giveaway  ", store.GetString("DisplayName_countdown2", ""));
        Assert.Equal("\uE7C8", store.GetString("IconGlyph_countdown2", ""));
        Assert.Equal("10,20,400,160", s.PopOutBounds);

        s.DisplayName = "";
        s.IconGlyph = "";
        Assert.Equal("Countdown 2", s.EffectiveTitle);
        Assert.Equal(TimerKind.Countdown2.DefaultIconGlyph(), s.EffectiveIconGlyph);
        Assert.Equal(20, TimerKindExtensions.IconChoices.Count);
        Assert.All(TimerKindExtensions.IconChoices, c => Assert.False(string.IsNullOrEmpty(c.Label)));
    }

    [Fact]
    public void InMemory_store_contains_remove_and_type_mismatch()
    {
        var store = new InMemorySettingsStore();
        Assert.False(store.Contains("a"));
        store.Set("a", 5L);
        Assert.True(store.Contains("a"));
        Assert.Equal(5L, store.GetLong("a", 0));
        Assert.Equal(-1, store.GetInt("a", -1)); // wrong type falls back to default
        store.Set("d", 1.5);
        Assert.Equal(1.5, store.GetDouble("d", 0));
        store.Remove("a");
        Assert.False(store.Contains("a"));
        Assert.Single(store.Values);
    }

    [Fact]
    public void ProductIds_and_entitlement_helpers()
    {
        Assert.True(ProductIds.IsSubscription(ProductIds.SubMonthly));
        Assert.True(ProductIds.IsSubscription(ProductIds.SubSixMonths));
        Assert.False(ProductIds.IsSubscription(ProductIds.Gold));
        Assert.Equal(5, ProductIds.All.Count);
        Assert.Equal(3, ProductIds.Lifetime.Count);
        Assert.Equal(2, ProductIds.Subscriptions.Count);
        Assert.Equal([ProductIds.Gold, ProductIds.SubMonthly, ProductIds.SubSixMonths], ProductIds.Purchasable);
        Assert.DoesNotContain(ProductIds.Bronze, ProductIds.Purchasable); // legacy: still an entitlement, no longer sold

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc), ProEntitlement.AddSubTime(baseDate));
        Assert.Equal(new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc), ProEntitlement.AddSubTime(baseDate, 6));

        var g = new GlobalSettings(new InMemorySettingsStore(), "x");
        var pro = new ProEntitlement(g) { ForceProInDebug = false };
        var changed = 0;
        pro.Changed += (_, _) => changed++;
        pro.ApplyLifetime("unknown-product");
        Assert.Equal(0, changed);
        Assert.False(pro.HasLifetime);
        pro.ApplyLifetime(ProductIds.Silver);
        Assert.True(pro.HasLifetime);
        Assert.Equal(1, changed);
        pro.NotifyChanged();
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Service_defaults_behave()
    {
        Assert.InRange((SystemClock.Instance.Now - DateTime.Now).TotalSeconds, -1, 1);
        Assert.Equal(DateTimeKind.Utc, SystemClock.Instance.UtcNow.Kind);

        var p = new NullTimerPlatform();
        Assert.False(p.HasRunningTimers);
        p.StartActivity("a");
        Assert.True(p.HasRunningTimers);
        p.StopActivity("a");
        Assert.False(p.HasRunningTimers);
        Assert.True(p.BeepAsync().IsCompletedSuccessfully);

        var none = UrlCommand.None;
        Assert.False(none.IsValid);
        Assert.Null(none.Kind);
        Assert.True(new UrlCommand(CommandAction.Pause, -1, "countdown3").IsValid);
        Assert.False(new UrlCommand(CommandAction.Pause, -1, "nope").IsValid);
    }
}

