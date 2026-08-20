using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Settings;

/// <summary>
/// Per-timer settings. Keys are <c>{legacyKey}_{id}</c>, identical to the legacy <c>Settings</c> class.
/// </summary>
public sealed class TimerSettings
{
    const string MinutesKey = "key_minutes";
    const string SecondsKey = "key_seconds";
    const string OutputKey = "key_output";
    const string FinishKey = "key_finish";
    const string FileNameKey = "key_file_name";
    const string AutoStartKey = "key_auto_start";
    const string MakeSoundKey = "make_sound";
    const string ShowAmPmKey = "key_show_ampm";
    const string OutputStyleKey = "key_output_style";
    const string UseMinutesKey = "UseMinutes";
    const string FinishAtTimeKey = "FinishAtTime";

    readonly ISettingsStore store;
    readonly string id;

    public TimerSettings(ISettingsStore store, TimerKind kind)
    {
        this.store = store;
        Kind = kind;
        id = kind.Id();
    }

    public TimerKind Kind { get; }

    string Key(string name) => $"{name}_{id}";

    public int Minutes { get => store.GetInt(Key(MinutesKey), Kind.DefaultMinutes()); set => store.Set(Key(MinutesKey), value); }
    public int Seconds { get => store.GetInt(Key(SecondsKey), 0); set => store.Set(Key(SecondsKey), value); }
    public string Output { get => store.GetString(Key(OutputKey), Kind.DefaultOutput()); set => store.Set(Key(OutputKey), value); }
    public string Finish { get => store.GetString(Key(FinishKey), TimerKindExtensions.DefaultFinishText); set => store.Set(Key(FinishKey), value); }
    public string FileName { get => store.GetString(Key(FileNameKey), Kind.DefaultFileName()); set => store.Set(Key(FileNameKey), value); }
    public bool AutoStart { get => store.GetBool(Key(AutoStartKey), false); set => store.Set(Key(AutoStartKey), value); }
    public bool MakeSound { get => store.GetBool(Key(MakeSoundKey), false); set => store.Set(Key(MakeSoundKey), value); }
    public bool ShowAmPm { get => store.GetBool(Key(ShowAmPmKey), false); set => store.Set(Key(ShowAmPmKey), value); }
    public int OutputStyle { get => store.GetInt(Key(OutputStyleKey), 0); set => store.Set(Key(OutputStyleKey), value); }
    public bool UseMinutes { get => store.GetBool(Key(UseMinutesKey), true); set => store.Set(Key(UseMinutesKey), value); }

    /// <summary>
    /// Time-of-day to finish at. Stored as <see cref="TimeSpan.Ticks"/> (long); <c>-1</c>/missing means
    /// "not set" and yields now + 15 minutes, matching legacy behaviour.
    /// </summary>
    public TimeSpan FinishAtTime
    {
        get
        {
            var ticks = store.GetLong(Key(FinishAtTimeKey), -1);
            if (ticks == -1)
            {
                var now = DateTime.Now;
                return new TimeSpan(now.Hour, now.Minute + 15, 0);
            }
            return new TimeSpan(ticks);
        }
        set => store.Set(Key(FinishAtTimeKey), value.Ticks);
    }

    // ----- New in 3.0 -----
    public string PopOutBounds { get => store.GetString(Key("PopOutBounds"), string.Empty); set => store.Set(Key("PopOutBounds"), value); }
}
