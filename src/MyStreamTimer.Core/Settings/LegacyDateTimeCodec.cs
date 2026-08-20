using System.Globalization;

namespace MyStreamTimer.Core.Settings;

/// <summary>
/// Encodes <see cref="DateTime"/> values exactly like Xam.Plugins.Settings did on UWP:
/// stored as a <b>string</b> containing the negated UTC ticks (negative = UTC). A positive value is a
/// legacy local-time tick count written before the plugin switched to UTC.
/// </summary>
public static class LegacyDateTimeCodec
{
    public static string Encode(DateTime value) =>
        Convert.ToString(-value.ToUniversalTime().Ticks, CultureInfo.InvariantCulture);

    public static DateTime? Decode(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            return null;

        return ticks >= 0
            ? new DateTime(ticks)
            : new DateTime(-ticks, DateTimeKind.Utc);
    }
}

public static class SettingsStoreDateTimeExtensions
{
    public static DateTime GetDateTime(this ISettingsStore store, string key, DateTime defaultValue) =>
        LegacyDateTimeCodec.Decode(store.GetString(key, string.Empty)) ?? defaultValue;

    public static void Set(this ISettingsStore store, string key, DateTime value) =>
        store.Set(key, LegacyDateTimeCodec.Encode(value));
}
