using System.Text.Json;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Settings;

public class LegacySettingsTests
{
    static InMemorySettingsStore LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-settings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var store = new InMemorySettingsStore();
        var local = doc.RootElement.EnumerateObject().First(p => p.Name.EndsWith("LocalState", StringComparison.Ordinal)).Value;
        foreach (var prop in local.EnumerateObject())
        {
            var type = prop.Value.GetProperty("type").GetString();
            var v = prop.Value.GetProperty("value");
            switch (type)
            {
                case "Boolean": store.Set(prop.Name, v.GetBoolean()); break;
                case "Int32": store.Set(prop.Name, v.GetInt32()); break;
                case "Int64": store.Set(prop.Name, v.GetInt64()); break;
                case "Double": store.Set(prop.Name, v.GetDouble()); break;
                case "String": store.Set(prop.Name, v.GetString()!); break;
            }
        }
        return store;
    }

    [Fact]
    public void Reads_captured_legacy_settings_dat_values()
    {
        var store = LoadFixture();
        var global = new GlobalSettings(store, @"C:\default");
        Assert.True(global.StayOnTop);
        Assert.Equal(1, global.TimesUsed);

        var cd = new TimerSettings(store, TimerKind.Countdown);
        Assert.Equal(5, cd.Minutes);
        Assert.Equal(@"Starting in {0:hh\:mm\:ss}", cd.Output);
        Assert.Equal("Let's do this!", cd.Finish);
        Assert.Equal("countdown.txt", cd.FileName);

        var up = new TimerSettings(store, TimerKind.Countup);
        Assert.Equal(@"{0:hh\:mm\:ss}", up.Output);
        Assert.Equal("countup.txt", up.FileName);
        Assert.Equal(0, up.Minutes);

        Assert.Equal("time.txt", new TimerSettings(store, TimerKind.Time).FileName);
    }

    [Fact]
    public void Defaults_match_legacy_when_keys_missing()
    {
        var store = new InMemorySettingsStore();
        var global = new GlobalSettings(store, @"C:\default\MyStreamTimer");
        Assert.Equal(@"C:\default\MyStreamTimer", global.DirectoryPath);
        Assert.True(global.StayOnTop);
        Assert.True(global.CheckSubStatus);
        Assert.False(global.IsSubValid);

        foreach (var kind in TimerKindExtensions.All)
        {
            var s = new TimerSettings(store, kind);
            Assert.Equal(kind.IsCountdown() ? 5 : 0, s.Minutes);
            Assert.Equal(0, s.Seconds);
            Assert.Equal(kind.IsCountUp() ? @"{0:hh\:mm\:ss}" : @"Starting in {0:hh\:mm\:ss}", s.Output);
            Assert.Equal("Let's do this!", s.Finish);
            Assert.Equal($"{kind.Id()}.txt", s.FileName);
            Assert.True(s.UseMinutes);
            Assert.False(s.AutoStart);
            Assert.False(s.MakeSound);
            Assert.Equal(0, s.OutputStyle);
        }
    }

    [Fact]
    public void FinishAtTime_uses_ticks_and_minus_one_sentinel()
    {
        var store = new InMemorySettingsStore();
        var s = new TimerSettings(store, TimerKind.Countdown2);
        store.Set("FinishAtTime_countdown2", -1L);
        var now = DateTime.Now;
        var v = s.FinishAtTime;
        Assert.InRange((v - now.TimeOfDay).TotalMinutes, 14, 16);

        s.FinishAtTime = new TimeSpan(15, 30, 0);
        Assert.Equal(new TimeSpan(15, 30, 0).Ticks, store.GetLong("FinishAtTime_countdown2", 0));
        Assert.Equal(new TimeSpan(15, 30, 0), s.FinishAtTime);
    }

    [Fact]
    public void DateTime_codec_matches_plugin_settings_encoding()
    {
        var dt = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        var encoded = LegacyDateTimeCodec.Encode(dt);
        Assert.Equal((-dt.Ticks).ToString(), encoded);
        Assert.Equal(dt, LegacyDateTimeCodec.Decode(encoded));
        Assert.Equal(DateTimeKind.Utc, LegacyDateTimeCodec.Decode(encoded)!.Value.Kind);

        // legacy positive ticks = local time
        var local = new DateTime(2020, 5, 15, 9, 0, 0);
        Assert.Equal(local, LegacyDateTimeCodec.Decode(local.Ticks.ToString()));
        Assert.Null(LegacyDateTimeCodec.Decode(""));
        Assert.Null(LegacyDateTimeCodec.Decode("nope"));
    }

    [Fact]
    public void Pro_formula_matches_legacy()
    {
        var store = new InMemorySettingsStore();
        var g = new GlobalSettings(store, "x");
        var pro = new ProEntitlement(g) { ForceProInDebug = false };
        Assert.False(pro.IsPro);

        g.IsGold = true;
        Assert.True(pro.IsPro);
        g.IsGold = false;

        g.HasTippedSub = true;
        g.SubExpirationDate = DateTime.UtcNow.AddDays(-1);
        Assert.False(pro.IsPro);
        g.SubExpirationDate = DateTime.UtcNow.AddDays(10);
        Assert.True(pro.IsPro);

        // stored encoding must be the legacy string form
        Assert.StartsWith("-", store.GetString("SubExpirationDate", ""));
    }

    [Fact]
    public void Sync_never_clears_lifetime_flags()
    {
        var store = new InMemorySettingsStore();
        var g = new GlobalSettings(store, "x") { IsBronze = true };
        var pro = new ProEntitlement(g) { ForceProInDebug = false };
        pro.Sync([], null);
        Assert.True(g.IsBronze);
        pro.Sync([ProductIds.Gold], DateTime.UtcNow.AddMonths(1));
        Assert.True(g.IsGold);
        Assert.True(g.HasTippedSub);
        Assert.True(pro.HasActiveSubscription);
    }
}
