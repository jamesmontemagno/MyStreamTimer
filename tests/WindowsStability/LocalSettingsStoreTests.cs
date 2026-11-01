// Included by MyStreamTimer.Core.Tests with the exact production LocalSettingsStore.cs.
// The stubs exercise managed conversion, not Windows.Storage or WinUI runtime behavior.
using System.IO.Compression;
using MyStreamTimer.Core.Diagnostics;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;
using Xunit;

namespace MyStreamTimer.WindowsStability.Tests
{
    [Collection("Windows stability")]
    public sealed class LocalSettingsStoreTests
    {
        public static TheoryData<object, bool> ValidValues => new()
        {
            { true, true }, { false, false }, { "true", true }, { " FALSE ", false },
            { "1", true }, { "0", false }, { "-2", true },
            { 0, false }, { -1, true }, { 1L, true }, { 0L, false },
            { 0.0, false }, { 1.0, true }, { 0.5, false }, { 1.5, true },
        };

        public static TheoryData<object, Type> InvalidValues => new()
        {
            { "not-a-bool", typeof(FormatException) },
            { "", typeof(FormatException) },
            { "9223372036854775808", typeof(OverflowException) },
            { ulong.MaxValue, typeof(OverflowException) },
            { double.NaN, typeof(OverflowException) },
            { double.PositiveInfinity, typeof(OverflowException) },
            { double.NegativeInfinity, typeof(OverflowException) },
            { DateTime.UnixEpoch, typeof(InvalidCastException) },
        };

        [Theory]
        [MemberData(nameof(ValidValues))]
        public void Valid_legacy_values_keep_their_existing_meaning(object value, bool expected)
        {
            Reset();
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["flag"] = value;
            var store = new LocalSettingsStore();
            Assert.Equal(expected, store.GetBool("flag", !expected));
            Assert.Empty(AppDiagnostics.Records);
        }

        [Theory]
        [MemberData(nameof(InvalidValues))]
        public void Invalid_conversion_uses_each_callers_default_and_records_the_failure(object value, Type exceptionType)
        {
            Reset();
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            values["flag"] = value;
            var store = new LocalSettingsStore();
            Assert.False(store.GetBool("flag", false));
            Assert.True(store.GetBool("flag", true));
            Assert.Same(value, values["flag"]);
            Assert.Equal(2, AppDiagnostics.Records.Count);
            Assert.All(AppDiagnostics.Records, record =>
            {
                Assert.Equal("settings-boolean-invalid", record.Event);
                Assert.Equal(exceptionType, record.Exception?.GetType());
                Assert.Equal("A stored Boolean could not be converted; using the caller default.", record.Details);
            });
        }

        [Fact]
        public void Missing_and_null_values_use_defaults_without_diagnostics()
        {
            Reset();
            var store = new LocalSettingsStore();
            Assert.False(store.GetBool("missing", false));
            Assert.True(store.GetBool("missing", true));
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["null"] = null!;
            Assert.True(store.GetBool("null", true));
            Assert.Empty(AppDiagnostics.Records);
        }

        [Fact]
        public void Startup_settings_remain_readable_without_rewriting_valid_lifetime_flags()
        {
            Reset();
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            values["key_auto_start_countdown"] = "invalid-autostart";
            values["UseMinutes_countdown"] = "invalid-mode";
            values["StayOnTop"] = "invalid-window-preference";
            values["IsBronze"] = "invalid-entitlement";
            values["IsGold"] = true;
            var store = new LocalSettingsStore();
            var timer = new TimerSettings(store, TimerKind.Countdown);
            var global = new GlobalSettings(store, "unused");
            Assert.False(timer.AutoStart);
            Assert.True(timer.UseMinutes);
            Assert.False(global.StayOnTop);
            Assert.False(global.IsBronze);
            Assert.True(new ProEntitlement(global) { ForceProInDebug = false }.IsPro);
            Assert.Equal("invalid-autostart", values["key_auto_start_countdown"]);
            Assert.Equal(true, values["IsGold"]);
        }

        [Fact]
        public void Property_store_failures_are_not_misclassified_as_conversion_failures()
        {
            Reset();
            var failure = new InvalidOperationException("Storage unavailable");
            Windows.Storage.ApplicationData.Current.LocalSettings.Values.ReadFailure = failure;
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new LocalSettingsStore().GetBool("flag", false)));
            Assert.Empty(AppDiagnostics.Records);
            Reset();
        }

        [Fact]
        public void Conversion_diagnostics_export_excludes_setting_keys_values_and_exception_messages()
        {
            Reset();
            const string key = "PRIVATE_setting_key";
            const string value = "PRIVATE_setting_value";
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] = value;
            Assert.False(new LocalSettingsStore().GetBool(key, false));
            var record = Assert.Single(AppDiagnostics.Records);
            Assert.Contains(value, Assert.IsType<FormatException>(record.Exception).Message);

            var directory = Path.Combine(Path.GetTempPath(), $"mst-settings-test-{Guid.NewGuid():N}");
            try
            {
                var log = new DiagnosticLog(directory);
                log.Write(record.Event, record.Details, record.Exception);
                var destination = Path.Combine(directory, "report.zip");
                Assert.True(log.TryExport(destination, out var error));
                Assert.Null(error);
                using var archive = ZipFile.OpenRead(destination);
                using var reader = new StreamReader(Assert.Single(archive.Entries).Open());
                var text = reader.ReadToEnd();
                Assert.Contains("settings-boolean-invalid", text);
                Assert.Contains("System.FormatException", text);
                Assert.DoesNotContain("PRIVATE", text);
                Assert.DoesNotContain(record.Exception.Message, text);
                Assert.DoesNotContain(".cs:", text);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private static void Reset()
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();
            Windows.Storage.ApplicationData.Current.LocalSettings.Values.ReadFailure = null;
            AppDiagnostics.Records.Clear();
        }
    }
}

#if WINDOWS_STABILITY_STUBS
namespace Windows.Foundation.Collections
{
    public interface IPropertySet
    {
        object this[string key] { get; set; }
        bool ContainsKey(string key);
        bool Remove(string key);
        bool TryGetValue(string key, out object value);
    }

    public sealed class TestPropertySet : Dictionary<string, object>, IPropertySet
    {
        public Exception? ReadFailure { get; set; }
        public new bool TryGetValue(string key, out object value)
        {
            if (ReadFailure is { } failure)
                throw failure;
            return base.TryGetValue(key, out value!);
        }
    }
}

namespace Windows.Storage
{
    public sealed class ApplicationData
    {
        public static ApplicationData Current { get; } = new();
        public ApplicationDataContainer LocalSettings { get; } = new();
    }

    public sealed class ApplicationDataContainer
    {
        public Windows.Foundation.Collections.TestPropertySet Values { get; } = new();
    }
}

namespace MyStreamTimer.WinUI.Services
{
    public static class AppDiagnostics
    {
        public static List<(string Event, string? Details, Exception? Exception)> Records { get; } = [];
        public static void Record(string eventName, string? details = null, Exception? exception = null) =>
            Records.Add((eventName, details, exception));
    }
}
#endif
