using MyStreamTimer.Core.Settings;

namespace MyStreamTimer.Core.Tests.Settings;

public class EndSoundSettingsTests
{
    [Fact]
    public void Existing_users_default_to_bundled_beep_without_changing_timer_switches()
    {
        var store = new InMemorySettingsStore();
        store.Set("make_sound_countdown", true);
        store.Set("make_sound_countdown2", false);

        var settings = new GlobalSettings(store, "output");

        Assert.Equal(EndSounds.Default, settings.EndSound);
        Assert.Empty(settings.CustomEndSoundPath);
        Assert.True(store.GetBool("make_sound_countdown", false));
        Assert.False(store.GetBool("make_sound_countdown2", true));
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
        var settings = new GlobalSettings(store, "output")
        {
            EndSound = sound,
            CustomEndSoundPath = @"C:\Sounds\My alert.mp3",
        };

        var reloaded = new GlobalSettings(store, "output");
        Assert.Equal(sound, reloaded.EndSound);
        Assert.Equal(settings.CustomEndSoundPath, reloaded.CustomEndSoundPath);
        Assert.Equal(sound, store.GetString("EndSound", ""));
        Assert.Equal(settings.CustomEndSoundPath, store.GetString("CustomEndSoundPath", ""));

        reloaded.EndSound = EndSounds.Default;
        Assert.Equal(settings.CustomEndSoundPath, reloaded.CustomEndSoundPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("../untrusted.wav")]
    public void Unknown_choices_fall_back_to_default(string sound)
    {
        var store = new InMemorySettingsStore();
        store.Set("EndSound", sound);
        var settings = new GlobalSettings(store, "output");

        Assert.Equal(EndSounds.Default, settings.EndSound);
        Assert.Equal("end-default.wav", EndSounds.GetBuiltInFileName(sound));
        settings.EndSound = sound;
        Assert.Equal(EndSounds.Default, store.GetString("EndSound", ""));
    }

    [Fact]
    public void Clearing_settings_restores_default_sound_and_removes_custom_path()
    {
        var store = new InMemorySettingsStore();
        var settings = new GlobalSettings(store, "output")
        {
            EndSound = EndSounds.Custom,
            CustomEndSoundPath = "alert.wav",
        };
        store.Remove(nameof(GlobalSettings.EndSound));
        store.Remove(nameof(GlobalSettings.CustomEndSoundPath));

        Assert.Equal(EndSounds.Default, settings.EndSound);
        Assert.Empty(settings.CustomEndSoundPath);
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
