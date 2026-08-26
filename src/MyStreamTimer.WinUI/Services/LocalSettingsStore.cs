using System.Globalization;
using MyStreamTimer.Core.Settings;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// <see cref="ISettingsStore"/> over <c>ApplicationData.Current.LocalSettings.Values</c> (root container,
/// no prefixes) so every key the legacy UWP app wrote through Plugin.Settings is read back unchanged.
/// Numeric reads tolerate a value stored as a different numeric type (e.g. an Int32 read as long).
/// </summary>
public sealed class LocalSettingsStore : ISettingsStore
{
    private readonly IPropertySet _values = ApplicationData.Current.LocalSettings.Values;

    public bool Contains(string key) => _values.ContainsKey(key);

    public void Remove(string key) => _values.Remove(key);

    public bool GetBool(string key, bool defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw is null)
        {
            return defaultValue;
        }

        return raw switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            IConvertible c => Convert.ToInt64(c, CultureInfo.InvariantCulture) != 0,
            _ => defaultValue,
        };
    }

    public int GetInt(string key, int defaultValue) => GetNumber(key, defaultValue, Convert.ToInt32);

    public long GetLong(string key, long defaultValue) => GetNumber(key, defaultValue, Convert.ToInt64);

    public double GetDouble(string key, double defaultValue) => GetNumber(key, defaultValue, Convert.ToDouble);

    public string GetString(string key, string defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw is null)
        {
            return defaultValue;
        }

        return raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? defaultValue;
    }

    public void Set(string key, bool value) => _values[key] = value;

    public void Set(string key, int value) => _values[key] = value;

    public void Set(string key, long value) => _values[key] = value;

    public void Set(string key, double value) => _values[key] = value;

    public void Set(string key, string value) => _values[key] = value;

    private T GetNumber<T>(string key, T defaultValue, Func<object, IFormatProvider, T> convert)
    {
        if (!_values.TryGetValue(key, out var raw) || raw is null)
        {
            return defaultValue;
        }

        try
        {
            return raw switch
            {
                T typed => typed,
                string s when s.Length > 0 => convert(s, CultureInfo.InvariantCulture),
                bool b => convert(b ? 1 : 0, CultureInfo.InvariantCulture),
                IConvertible => convert(raw, CultureInfo.InvariantCulture),
                _ => defaultValue,
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return defaultValue;
        }
    }
}
