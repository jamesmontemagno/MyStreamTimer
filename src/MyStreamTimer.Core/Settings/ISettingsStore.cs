namespace MyStreamTimer.Core.Settings;

/// <summary>
/// Minimal key/value store abstraction. The Windows implementation targets
/// <c>ApplicationData.Current.LocalSettings.Values</c> (root container) so that every value written by
/// the legacy Xamarin.Forms/UWP app (via Plugin.Settings) is readable without migration.
/// Only the primitive types the legacy plugin used are supported.
/// </summary>
public interface ISettingsStore
{
    bool Contains(string key);
    void Remove(string key);

    bool GetBool(string key, bool defaultValue);
    int GetInt(string key, int defaultValue);
    long GetLong(string key, long defaultValue);
    double GetDouble(string key, double defaultValue);
    string GetString(string key, string defaultValue);

    void Set(string key, bool value);
    void Set(string key, int value);
    void Set(string key, long value);
    void Set(string key, double value);
    void Set(string key, string value);
}

/// <summary>In-memory store for tests and previews.</summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    readonly Dictionary<string, object> values = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object> Values => values;

    public bool Contains(string key) => values.ContainsKey(key);
    public void Remove(string key) => values.Remove(key);

    T Get<T>(string key, T defaultValue) => values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;

    public bool GetBool(string key, bool defaultValue) => Get(key, defaultValue);
    public int GetInt(string key, int defaultValue) => Get(key, defaultValue);
    public long GetLong(string key, long defaultValue) => Get(key, defaultValue);
    public double GetDouble(string key, double defaultValue) => Get(key, defaultValue);
    public string GetString(string key, string defaultValue) => Get(key, defaultValue);

    public void Set(string key, bool value) => values[key] = value;
    public void Set(string key, int value) => values[key] = value;
    public void Set(string key, long value) => values[key] = value;
    public void Set(string key, double value) => values[key] = value;
    public void Set(string key, string value) => values[key] = value;
}
