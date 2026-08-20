namespace MyStreamTimer.Core.Settings;

/// <summary>
/// Global (non per-timer) settings. Key names and defaults are identical to the legacy
/// <c>GlobalSettings</c> class so existing users keep their configuration and Pro unlocks.
/// </summary>
public sealed class GlobalSettings
{
    public const string DirectoryPathKey = "global_directory_path";

    readonly ISettingsStore store;
    readonly string defaultDirectoryPath;

    public GlobalSettings(ISettingsStore store, string defaultDirectoryPath)
    {
        this.store = store;
        this.defaultDirectoryPath = defaultDirectoryPath;
    }

    public string DefaultDirectoryPath => defaultDirectoryPath;

    public string DirectoryPath
    {
        get => store.GetString(DirectoryPathKey, defaultDirectoryPath);
        set => store.Set(DirectoryPathKey, value);
    }

    public int TimesUsed
    {
        get => store.GetInt(nameof(TimesUsed), 0);
        set => store.Set(nameof(TimesUsed), value);
    }

    public bool IsBronze { get => store.GetBool(nameof(IsBronze), false); set => store.Set(nameof(IsBronze), value); }
    public bool IsSilver { get => store.GetBool(nameof(IsSilver), false); set => store.Set(nameof(IsSilver), value); }
    public bool IsGold { get => store.GetBool(nameof(IsGold), false); set => store.Set(nameof(IsGold), value); }

    public bool CheckSubStatus { get => store.GetBool(nameof(CheckSubStatus), true); set => store.Set(nameof(CheckSubStatus), value); }
    public string SubPrice { get => store.GetString(nameof(SubPrice), string.Empty); set => store.Set(nameof(SubPrice), value); }
    public string SubPrice6Months { get => store.GetString(nameof(SubPrice6Months), string.Empty); set => store.Set(nameof(SubPrice6Months), value); }
    public bool HasTippedSub { get => store.GetBool(nameof(HasTippedSub), false); set => store.Set(nameof(HasTippedSub), value); }
    public bool ShowSupportPopUp { get => store.GetBool(nameof(ShowSupportPopUp), true); set => store.Set(nameof(ShowSupportPopUp), value); }

    public DateTime SubExpirationDate
    {
        get => store.GetDateTime(nameof(SubExpirationDate), DateTime.UtcNow.AddDays(-1));
        set => store.Set(nameof(SubExpirationDate), value);
    }

    public bool IsSubValid => SubExpirationDate > DateTime.UtcNow;

    public string ProPrice { get => store.GetString(nameof(ProPrice), string.Empty); set => store.Set(nameof(ProPrice), value); }

    public DateTime ProPriceDate
    {
        get => store.GetDateTime(nameof(ProPriceDate), DateTime.UtcNow);
        set => store.Set(nameof(ProPriceDate), value);
    }

    /// <summary>Windows default was <c>true</c> (macOS was false).</summary>
    public bool StayOnTop { get => store.GetBool(nameof(StayOnTop), true); set => store.Set(nameof(StayOnTop), value); }

    public bool FirstRun { get => store.GetBool(nameof(FirstRun), true); set => store.Set(nameof(FirstRun), value); }

    // ----- New in 3.0 (ported from the SwiftUI app) -----

    public const string DefaultPopOutTextColorHex = "#FFFFFF";
    public const string DefaultPopOutBackgroundColorHex = "#000000";
    public const double DefaultPopOutFontSize = 48;

    public string AppTheme { get => store.GetString(nameof(AppTheme), "system"); set => store.Set(nameof(AppTheme), value); }
    public double PopOutFontSize { get => store.GetDouble(nameof(PopOutFontSize), DefaultPopOutFontSize); set => store.Set(nameof(PopOutFontSize), value); }
    public string PopOutFontFamily { get => store.GetString(nameof(PopOutFontFamily), string.Empty); set => store.Set(nameof(PopOutFontFamily), value); }
    public string PopOutTextColorHex { get => store.GetString(nameof(PopOutTextColorHex), DefaultPopOutTextColorHex); set => store.Set(nameof(PopOutTextColorHex), value); }
    public string PopOutBackgroundColorHex { get => store.GetString(nameof(PopOutBackgroundColorHex), DefaultPopOutBackgroundColorHex); set => store.Set(nameof(PopOutBackgroundColorHex), value); }
    public bool HasSeenWelcomeBackV1 { get => store.GetBool(nameof(HasSeenWelcomeBackV1), false); set => store.Set(nameof(HasSeenWelcomeBackV1), value); }
    public string LastSelectedPage { get => store.GetString(nameof(LastSelectedPage), string.Empty); set => store.Set(nameof(LastSelectedPage), value); }
    public string MainWindowBounds { get => store.GetString(nameof(MainWindowBounds), string.Empty); set => store.Set(nameof(MainWindowBounds), value); }
}
