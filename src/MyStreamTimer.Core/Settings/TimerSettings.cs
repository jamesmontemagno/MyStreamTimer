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
    public const string EndSoundKey = "key_end_sound";
    public const string CustomEndSoundPathKey = "key_custom_end_sound_path";
    public const string SoundAtMinutesKey = "key_sound_minutes";
    public const string SoundAtSecondsKey = "key_sound_seconds";
    public const int DefaultSoundAtMinutes = 5;

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

    /// <summary>User-defined display name; empty means the default <see cref="TimerKindExtensions.Title"/>.</summary>
    public string DisplayName { get => store.GetString(Key("DisplayName"), string.Empty); set => store.Set(Key("DisplayName"), value); }

    /// <summary>User-chosen Segoe Fluent Icons glyph (e.g. "\uE916"); empty means the default glyph for the kind.</summary>
    public string IconGlyph { get => store.GetString(Key("IconGlyph"), string.Empty); set => store.Set(Key("IconGlyph"), value); }

    /// <summary>End sound played by <see cref="MakeSound"/> (countdown at zero, count-up at <see cref="SoundAt"/>).</summary>
    public string EndSound { get => EndSounds.Normalize(store.GetString(Key(EndSoundKey), EndSounds.Default)); set => store.Set(Key(EndSoundKey), EndSounds.Normalize(value)); }
    public string CustomEndSoundPath { get => store.GetString(Key(CustomEndSoundPathKey), string.Empty); set => store.Set(Key(CustomEndSoundPathKey), value ?? string.Empty); }
    public EndSoundSelection EndSoundSelection => new(EndSound, CustomEndSoundPath);

    /// <summary>Count-up only: elapsed time at which the end sound plays.</summary>
    public int SoundAtMinutes { get => Math.Max(0, store.GetInt(Key(SoundAtMinutesKey), DefaultSoundAtMinutes)); set => store.Set(Key(SoundAtMinutesKey), Math.Max(0, value)); }
    public int SoundAtSeconds { get => Math.Clamp(store.GetInt(Key(SoundAtSecondsKey), 0), 0, 59); set => store.Set(Key(SoundAtSecondsKey), Math.Clamp(value, 0, 59)); }
    public TimeSpan SoundAt => TimeSpan.FromMinutes(SoundAtMinutes) + TimeSpan.FromSeconds(SoundAtSeconds);

    public string EffectiveTitle => string.IsNullOrWhiteSpace(DisplayName) ? Kind.Title() : DisplayName.Trim();
    public string EffectiveIconGlyph => string.IsNullOrWhiteSpace(IconGlyph) ? Kind.DefaultIconGlyph() : IconGlyph;
}
