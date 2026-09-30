using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.Core.Tests.Settings;

public class EndSoundSettingsTests
{
    [Fact]
    public void Existing_users_default_to_bundled_beep_without_changing_timer_switches()
    {
        var store = new InMemorySettingsStore();
        store.Set("make_sound_countdown", true);
        store.Set("make_sound_countdown2", false);

        var first = new TimerSettings(store, TimerKind.Countdown);
        var second = new TimerSettings(store, TimerKind.Countdown2);

        Assert.Equal(EndSounds.Default, first.EndSound);
        Assert.Empty(first.CustomEndSoundPath);
        Assert.Equal(EndSoundSelection.Default, first.EndSoundSelection);
        Assert.True(first.MakeSound);
        Assert.False(second.MakeSound);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("chime")]
    [InlineData("bell")]
    [InlineData("digital")]
    [InlineData("custom")]
    public void Sound_choice_and_custom_file_survive_new_settings_instance(string sound)
    {
        var store = new InMemorySettingsStore();
        var settings = new TimerSettings(store, TimerKind.Countup)
        {
            EndSound = sound,
            CustomEndSoundPath = @"C:\Sounds\My alert.mp3",
        };

        var reloaded = new TimerSettings(store, TimerKind.Countup);
        Assert.Equal(sound, reloaded.EndSound);
        Assert.Equal(settings.CustomEndSoundPath, reloaded.CustomEndSoundPath);
        Assert.Equal(new EndSoundSelection(sound, settings.CustomEndSoundPath), reloaded.EndSoundSelection);
        Assert.Equal(sound, store.GetString("key_end_sound_countup", ""));
        Assert.Equal(settings.CustomEndSoundPath, store.GetString("key_custom_end_sound_path_countup", ""));

        reloaded.EndSound = EndSounds.Default;
        Assert.Equal(settings.CustomEndSoundPath, reloaded.CustomEndSoundPath);
    }

    [Fact]
    public void Each_timer_keeps_its_own_sound()
    {
        var store = new InMemorySettingsStore();
        new TimerSettings(store, TimerKind.Countdown) { EndSound = "bell" };
        new TimerSettings(store, TimerKind.Countdown2) { EndSound = EndSounds.Custom, CustomEndSoundPath = "a.mp3" };
        new TimerSettings(store, TimerKind.Countup) { EndSound = "chime", CustomEndSoundPath = "b.wav" };

        Assert.Equal(new EndSoundSelection("bell", ""), new TimerSettings(store, TimerKind.Countdown).EndSoundSelection);
        Assert.Equal(new EndSoundSelection(EndSounds.Custom, "a.mp3"), new TimerSettings(store, TimerKind.Countdown2).EndSoundSelection);
        Assert.Equal(new EndSoundSelection("chime", "b.wav"), new TimerSettings(store, TimerKind.Countup).EndSoundSelection);
        Assert.Equal(EndSoundSelection.Default, new TimerSettings(store, TimerKind.Countdown3).EndSoundSelection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("../untrusted.wav")]
    public void Unknown_choices_fall_back_to_default(string sound)
    {
        var store = new InMemorySettingsStore();
        store.Set("key_end_sound_countdown", sound);
        var settings = new TimerSettings(store, TimerKind.Countdown);

        Assert.Equal(EndSounds.Default, settings.EndSound);
        Assert.Equal("end-default.wav", EndSounds.GetBuiltInFileName(sound));
        settings.EndSound = sound;
        Assert.Equal(EndSounds.Default, store.GetString("key_end_sound_countdown", ""));
    }

    [Fact]
    public void Clearing_settings_restores_default_sound_and_removes_custom_path()
    {
        var store = new InMemorySettingsStore();
        var settings = new TimerSettings(store, TimerKind.Countdown4)
        {
            EndSound = EndSounds.Custom,
            CustomEndSoundPath = "alert.wav",
            SoundAtMinutes = 9,
            SoundAtSeconds = 30,
        };
        store.Remove($"{TimerSettings.EndSoundKey}_countdown4");
        store.Remove($"{TimerSettings.CustomEndSoundPathKey}_countdown4");
        store.Remove($"{TimerSettings.SoundAtMinutesKey}_countdown4");
        store.Remove($"{TimerSettings.SoundAtSecondsKey}_countdown4");

        Assert.Equal(EndSounds.Default, settings.EndSound);
        Assert.Empty(settings.CustomEndSoundPath);
        Assert.Equal(TimeSpan.FromMinutes(TimerSettings.DefaultSoundAtMinutes), settings.SoundAt);
    }

    [Fact]
    public void Count_up_sound_time_defaults_to_five_minutes_and_is_clamped()
    {
        var store = new InMemorySettingsStore();
        var settings = new TimerSettings(store, TimerKind.Countup2);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.SoundAt);

        settings.SoundAtMinutes = -3;
        settings.SoundAtSeconds = 75;
        Assert.Equal(0, settings.SoundAtMinutes);
        Assert.Equal(59, settings.SoundAtSeconds);

        settings.SoundAtMinutes = 12;
        settings.SoundAtSeconds = 5;
        Assert.Equal(new TimeSpan(0, 12, 5), new TimerSettings(store, TimerKind.Countup2).SoundAt);
    }

    [Fact]
    public void Catalog_has_unique_ids_and_only_custom_has_no_bundled_file()
    {
        Assert.Equal(5, EndSounds.Choices.Count);
        Assert.Equal(5, EndSounds.Choices.Select(choice => choice.Id).Distinct().Count());
        Assert.All(EndSounds.Choices, choice => Assert.False(string.IsNullOrWhiteSpace(choice.DisplayName)));
        Assert.Equal(EndSounds.Custom, Assert.Single(EndSounds.Choices, choice => choice.FileName is null).Id);
        Assert.All(EndSounds.Choices.Where(choice => choice.Id != EndSounds.Custom),
            choice => Assert.Equal($"end-{choice.Id}.wav", EndSounds.GetBuiltInFileName(choice.Id)));
        Assert.Equal("end-default.wav", EndSounds.GetBuiltInFileName(EndSounds.Custom));
        Assert.Equal("end-default.wav", EndSounds.GetBuiltInFileName(null));
    }

    [Theory]
    [InlineData("alert.mp3", true)]
    [InlineData("alert.MP3", true)]
    [InlineData("alert.wav", true)]
    [InlineData("alert.WAV", true)]
    [InlineData("alert.wave", true)]
    [InlineData("alert.WAVE", true)]
    [InlineData("alert.ogg", false)]
    [InlineData("alert.mp3.exe", false)]
    [InlineData("alert", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_mp3_and_wave_extensions_are_supported(string? path, bool expected)
    {
        Assert.Equal(expected, EndSounds.IsSupportedCustomFile(path));
    }
}
