namespace MyStreamTimer.Core.Settings;

public sealed record EndSoundChoice(string Id, string DisplayName, string? FileName);

public static class EndSounds
{
    public const string Default = "default";
    public const string Custom = "custom";

    public static IReadOnlyList<EndSoundChoice> Choices { get; } = Array.AsReadOnly<EndSoundChoice>(
    [
        new(Default, "Default beep", "end-default.wav"),
        new("chime", "Chime", "end-chime.wav"),
        new("bell", "Bell", "end-bell.wav"),
        new("digital", "Digital", "end-digital.wav"),
        new(Custom, "Custom", null),
    ]);

    public static string Normalize(string? id) => Choices.Any(choice => choice.Id == id) ? id! : Default;

    public static string GetBuiltInFileName(string? id) =>
        Choices.FirstOrDefault(choice => choice.Id == id)?.FileName ?? Choices[0].FileName!;

    public static bool IsSupportedCustomFile(string? path) =>
        Path.GetExtension(path)?.ToLowerInvariant() is ".mp3" or ".wav" or ".wave";
}
