using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Helpers;
using MyStreamTimer.WinUI.Services;
using Windows.UI;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>One row in the "output files" list: a timer's title and its full output path.</summary>
public sealed partial class TimerFileItem : ObservableObject
{
    public TimerFileItem(string title, string fullPath, IRelayCommand<string?> copyCommand)
    {
        Title = title;
        FullPath = fullPath;
        CopyCommand = copyCommand;
    }

    public string Title { get; }

    public string FullPath { get; }

    public IRelayCommand<string?> CopyCommand { get; }

    public string AutomationName => $"Copy path for {Title}";
}

/// <summary>Settings page: output folder, appearance (theme / stay on top), pop-out appearance (Pro) and data reset.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const string DefaultFontLabel = "Default (Segoe UI)";

    private static readonly string[] TimerKeyNames =
    [
        "key_minutes", "key_seconds", "key_output", "key_finish", "key_file_name", "key_auto_start", "make_sound",
        "key_show_ampm", "key_output_style", "UseMinutes", "FinishAtTime", "PopOutBounds", "DisplayName", "IconGlyph",
    ];

    private static readonly string[] GlobalKeyNames =
    [
        GlobalSettings.DirectoryPathKey, nameof(GlobalSettings.StayOnTop), nameof(GlobalSettings.AppTheme),
        nameof(GlobalSettings.PopOutFontSize), nameof(GlobalSettings.PopOutFontFamily),
        nameof(GlobalSettings.PopOutTextColorHex), nameof(GlobalSettings.PopOutBackgroundColorHex),
        nameof(GlobalSettings.LastSelectedPage), nameof(GlobalSettings.MainWindowBounds),
    ];

    private readonly GlobalSettings _settings;
    private readonly ISettingsStore _store;
    private readonly WindowService _windowService;
    private readonly FolderService _folders;
    private readonly ClipboardService _clipboard;
    private readonly LauncherService _launcher;
    private readonly DialogService _dialogs;
    private readonly ProEntitlement _pro;
    private readonly PopOutService _popOuts;
    private readonly TimerHost _timers;
    private bool _isLoading;

    public SettingsViewModel(GlobalSettings settings, ISettingsStore store, WindowService windowService, FolderService folders,
        ClipboardService clipboard, LauncherService launcher, DialogService dialogs, ProEntitlement pro, PopOutService popOuts,
        TimerHost timers)
    {
        _settings = settings;
        _store = store;
        _windowService = windowService;
        _folders = folders;
        _clipboard = clipboard;
        _launcher = launcher;
        _dialogs = dialogs;
        _pro = pro;
        _popOuts = popOuts;
        _timers = timers;

        FontOptions = [DefaultFontLabel, .. FontFamilies.GetSystemFontFamilies()];
        foreach (var kind in TimerKindExtensions.All)
        {
            TimerAppearances.Add(new TimerAppearanceItem(_timers.Engine(kind).Settings, OnTimerAppearanceChanged));
        }

        LoadFromSettings();
    }

    private void OnTimerAppearanceChanged(TimerKind kind)
    {
        _popOuts.NotifyTimerAppearanceChanged(kind);
        RefreshTimerFiles();
    }

    /// <summary>Raised when the page should navigate to the Pro page.</summary>
    public event EventHandler? NavigateToProRequested;

    // ---------- output folder ----------

    [ObservableProperty]
    public partial string DirectoryPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDefaultDirectory { get; set; }

    public ObservableCollection<TimerFileItem> TimerFiles { get; } = [];

    /// <summary>Per-timer display name / icon rows (Timers section).</summary>
    public ObservableCollection<TimerAppearanceItem> TimerAppearances { get; } = [];

    [ObservableProperty]
    public partial bool IsFolderStatusOpen { get; set; }

    [ObservableProperty]
    public partial string FolderStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity FolderStatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsFolderBusy { get; set; }

    // ---------- appearance ----------

    /// <summary>0 = System, 1 = Light, 2 = Dark (RadioButtons SelectedIndex).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool StayOnTop { get; set; }

    // ---------- pop-out appearance ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotPro))]
    public partial bool IsPro { get; set; }

    public bool IsNotPro => !IsPro;

    public IReadOnlyList<string> FontOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeLabel))]
    public partial double PopOutFontSize { get; set; } = GlobalSettings.DefaultPopOutFontSize;

    public string FontSizeLabel => $"{PopOutFontSize:0} pt";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontFamily))]
    public partial string SelectedFont { get; set; } = DefaultFontLabel;

    public FontFamily PreviewFontFamily => SelectedFont == DefaultFontLabel || string.IsNullOrWhiteSpace(SelectedFont)
        ? FontFamily.XamlAutoFontFamily
        : new FontFamily(SelectedFont);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextColorBrush))]
    [NotifyPropertyChangedFor(nameof(TextColorHex))]
    public partial Color TextColor { get; set; } = Colors.White;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColorBrush))]
    [NotifyPropertyChangedFor(nameof(BackgroundColorHex))]
    public partial Color BackgroundColor { get; set; } = Colors.Black;

    public SolidColorBrush TextColorBrush => new(TextColor);

    public SolidColorBrush BackgroundColorBrush => new(BackgroundColor);

    public string TextColorHex => ColorHex.ToHex(TextColor);

    public string BackgroundColorHex => ColorHex.ToHex(BackgroundColor);

    // ---------- lifecycle ----------

    public void Activate()
    {
        _pro.Changed += OnProChanged;
        IsPro = _pro.IsPro;
    }

    public void Deactivate() => _pro.Changed -= OnProChanged;

    private void OnProChanged(object? sender, EventArgs e) => App.DispatcherQueue.TryEnqueue(() => IsPro = _pro.IsPro);

    private void LoadFromSettings()
    {
        _isLoading = true;
        try
        {
            DirectoryPath = _settings.DirectoryPath;
            IsDefaultDirectory = string.Equals(DirectoryPath, _settings.DefaultDirectoryPath, StringComparison.OrdinalIgnoreCase);
            RefreshTimerFiles();

            ThemeIndex = _settings.AppTheme.ToLowerInvariant() switch
            {
                "light" => 1,
                "dark" => 2,
                _ => 0,
            };
            StayOnTop = _settings.StayOnTop;
            IsPro = _pro.IsPro;

            PopOutFontSize = Math.Clamp(_settings.PopOutFontSize, 12, 200);
            var family = _settings.PopOutFontFamily;
            SelectedFont = string.IsNullOrWhiteSpace(family) ? DefaultFontLabel
                : FontOptions.FirstOrDefault(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase)) ?? DefaultFontLabel;
            TextColor = ColorHex.Parse(_settings.PopOutTextColorHex, Colors.White);
            BackgroundColor = ColorHex.Parse(_settings.PopOutBackgroundColorHex, Colors.Black);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RefreshTimerFiles()
    {
        TimerFiles.Clear();
        foreach (var kind in TimerKindExtensions.All)
        {
            var settings = _timers.Engine(kind).Settings;
            TimerFiles.Add(new TimerFileItem(settings.EffectiveTitle, Path.Combine(DirectoryPath, settings.FileName), CopyTextCommand));
        }
    }

    private void SetDirectory(string path)
    {
        _settings.DirectoryPath = path;
        DirectoryPath = path;
        IsDefaultDirectory = string.Equals(path, _settings.DefaultDirectoryPath, StringComparison.OrdinalIgnoreCase);
        RefreshTimerFiles();
    }

    private void ShowFolderStatus(string message, InfoBarSeverity severity)
    {
        FolderStatusMessage = message;
        FolderStatusSeverity = severity;
        IsFolderStatusOpen = true;
    }

    // ---------- commands: folder ----------

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var path = await _folders.PickFolderAsync();
        if (path is null)
        {
            return;
        }

        IsFolderBusy = true;
        try
        {
            var (ok, message) = await _folders.TestAccessAsync(path);
            if (ok)
            {
                SetDirectory(path);
            }

            ShowFolderStatus(message, ok ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        finally
        {
            IsFolderBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] CreateDirectory failed: {ex.Message}");
        }

        if (!await _launcher.OpenFolderAsync(DirectoryPath))
        {
            ShowFolderStatus("Couldn't open the folder. Check that it exists and try Test access.", InfoBarSeverity.Warning);
        }
    }

    [RelayCommand]
    private async Task TestAccessAsync()
    {
        IsFolderBusy = true;
        try
        {
            var (ok, message) = await _folders.TestAccessAsync(DirectoryPath);
            ShowFolderStatus(message, ok ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        finally
        {
            IsFolderBusy = false;
        }
    }

    [RelayCommand]
    private async Task UseDefaultAsync()
    {
        IsFolderBusy = true;
        try
        {
            var path = _settings.DefaultDirectoryPath;
            var (ok, message) = await _folders.TestAccessAsync(path);
            if (ok)
            {
                SetDirectory(path);
                ShowFolderStatus("Output folder reset to the default location.", InfoBarSeverity.Success);
            }
            else
            {
                ShowFolderStatus(message, InfoBarSeverity.Error);
            }
        }
        finally
        {
            IsFolderBusy = false;
        }
    }

    [RelayCommand]
    private void CopyPath() => _clipboard.SetText(DirectoryPath);

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    // ---------- commands: pro / data ----------

    [RelayCommand]
    private void GoToPro() => NavigateToProRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Reset all settings?",
            "Every timer's duration, output format, file name and behaviour, plus the output folder, theme and pop-out appearance will return to their defaults. Your Pro purchases are kept.",
            "Reset",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        foreach (var kind in TimerKindExtensions.All)
        {
            foreach (var name in TimerKeyNames)
            {
                _store.Remove($"{name}_{kind.Id()}");
            }
        }

        foreach (var key in GlobalKeyNames)
        {
            _store.Remove(key);
        }

        _popOuts.CloseAll();
        LoadFromSettings();
        foreach (var item in TimerAppearances)
        {
            item.ResetCommand.Execute(null);
        }

        _windowService.ApplyTheme(_settings.AppTheme);
        _windowService.SetAlwaysOnTop(_settings.StayOnTop);
        _popOuts.NotifyAppearanceChanged();
        ShowFolderStatus("All settings were reset to their defaults.", InfoBarSeverity.Success);
    }

    // ---------- change handlers ----------

    partial void OnThemeIndexChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        var theme = value switch
        {
            1 => "light",
            2 => "dark",
            _ => "system",
        };
        _settings.AppTheme = theme;
        _windowService.ApplyTheme(theme);
    }

    partial void OnStayOnTopChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.StayOnTop = value;
        _windowService.SetAlwaysOnTop(value);
    }

    partial void OnPopOutFontSizeChanged(double value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.PopOutFontSize = Math.Round(value);
        _popOuts.NotifyAppearanceChanged();
    }

    partial void OnSelectedFontChanged(string value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.PopOutFontFamily = value == DefaultFontLabel ? string.Empty : value ?? string.Empty;
        _popOuts.NotifyAppearanceChanged();
    }

    partial void OnTextColorChanged(Color value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.PopOutTextColorHex = ColorHex.ToHex(value);
        _popOuts.NotifyAppearanceChanged();
    }

    partial void OnBackgroundColorChanged(Color value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.PopOutBackgroundColorHex = ColorHex.ToHex(value);
        _popOuts.NotifyAppearanceChanged();
    }
}
