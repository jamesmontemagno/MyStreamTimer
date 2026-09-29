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

/// <summary>Settings page: output folder, end sound, appearance, pop-out appearance (Pro) and data reset.</summary>
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
        nameof(GlobalSettings.EndSound), nameof(GlobalSettings.CustomEndSoundPath),
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
    private readonly BeepService _beep;
    private CancellationTokenSource _endSoundCancellation = new();
    private bool _isLoading;

    public SettingsViewModel(GlobalSettings settings, ISettingsStore store, WindowService windowService, FolderService folders,
        ClipboardService clipboard, LauncherService launcher, DialogService dialogs, ProEntitlement pro, PopOutService popOuts,
        TimerHost timers, BeepService beep)
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
        _beep = beep;

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

    // ---------- end sound ----------

    public IReadOnlyList<EndSoundChoice> EndSoundChoices => EndSounds.Choices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomEndSound))]
    public partial string SelectedEndSoundId { get; set; } = EndSounds.Default;

    public bool IsCustomEndSound => SelectedEndSoundId == EndSounds.Custom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomEndSoundStatus))]
    public partial string CustomEndSoundPath { get; set; } = string.Empty;

    public string CustomEndSoundStatus => string.IsNullOrWhiteSpace(CustomEndSoundPath)
        ? "No custom audio selected. The default beep will be used."
        : !File.Exists(CustomEndSoundPath)
            ? $"Unavailable: {Path.GetFileName(CustomEndSoundPath)}. The default beep will be used."
            : $"Selected: {Path.GetFileName(CustomEndSoundPath)}";

    [ObservableProperty]
    public partial bool IsEndSoundBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopEndSoundPreviewCommand))]
    public partial bool IsEndSoundPreviewing { get; set; }

    [ObservableProperty]
    public partial bool IsEndSoundStatusOpen { get; set; }

    [ObservableProperty]
    public partial string EndSoundStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity EndSoundStatusSeverity { get; set; } = InfoBarSeverity.Informational;

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
        OnPropertyChanged(nameof(CustomEndSoundStatus));
    }

    public void Deactivate()
    {
        _pro.Changed -= OnProChanged;
        CancelEndSoundOperations();
    }

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

            SelectedEndSoundId = _settings.EndSound;
            CustomEndSoundPath = _settings.CustomEndSoundPath;
            OnPropertyChanged(nameof(CustomEndSoundStatus));
            IsEndSoundStatusOpen = false;

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

    // ---------- commands: end sound ----------

    [RelayCommand]
    private async Task ChooseEndSoundFileAsync()
    {
        if (IsEndSoundBusy)
        {
            return;
        }

        var cancellationToken = _endSoundCancellation.Token;
        IsEndSoundBusy = true;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.Window.AppWindow.Id)
            {
                SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.MusicLibrary,
            };
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".wave");
            var result = await picker.PickSingleFileAsync();
            if (result is null || string.IsNullOrEmpty(result.Path) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!EndSounds.IsSupportedCustomFile(result.Path))
            {
                ShowEndSoundStatus("Choose an MP3 or WAV audio file. Your previous selection was kept.", InfoBarSeverity.Error);
                return;
            }

            var valid = await BeepService.ValidateCustomFileAsync(result.Path, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!valid)
            {
                ShowEndSoundStatus("This audio file couldn't be opened or decoded. Choose a playable MP3 or WAV file. Your previous selection was kept.", InfoBarSeverity.Error);
                return;
            }

            _settings.CustomEndSoundPath = result.Path;
            CustomEndSoundPath = result.Path;
            SelectedEndSoundId = EndSounds.Custom;
            ShowEndSoundStatus("Custom end sound saved. Keep the file in this location so it can be played.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Choose end sound failed: {ex.Message}");
            if (!cancellationToken.IsCancellationRequested)
            {
                ShowEndSoundStatus("Couldn't choose this audio file. Your previous selection was kept.", InfoBarSeverity.Error);
            }
        }
        finally
        {
            IsEndSoundBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewEndSoundAsync()
    {
        if (IsEndSoundBusy)
        {
            return;
        }

        var cancellationToken = _endSoundCancellation.Token;
        IsEndSoundBusy = true;
        IsEndSoundPreviewing = true;
        IsEndSoundStatusOpen = false;
        try
        {
            var result = await _beep.PreviewAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            OnPropertyChanged(nameof(CustomEndSoundStatus));
            if (result == EndSoundPlaybackResult.DefaultFallback)
            {
                ShowEndSoundStatus("The selected audio couldn't be played. The default beep was used instead.", InfoBarSeverity.Warning);
            }
            else if (result == EndSoundPlaybackResult.Unavailable)
            {
                ShowEndSoundStatus("The end sound couldn't be played. Check your audio output and try another file.", InfoBarSeverity.Error);
            }
        }
        finally
        {
            IsEndSoundPreviewing = false;
            IsEndSoundBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsEndSoundPreviewing))]
    private void StopEndSoundPreview() => CancelEndSoundOperations();

    private void ShowEndSoundStatus(string message, InfoBarSeverity severity)
    {
        EndSoundStatusMessage = message;
        EndSoundStatusSeverity = severity;
        IsEndSoundStatusOpen = true;
    }

    private void CancelEndSoundOperations()
    {
        _endSoundCancellation.Cancel();
        _endSoundCancellation.Dispose();
        _endSoundCancellation = new CancellationTokenSource();
        IsEndSoundPreviewing = false;
    }

    // ---------- commands: pro / data ----------

    [RelayCommand]
    private void GoToPro() => NavigateToProRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Reset all settings?",
            "Every timer's duration, output format, file name and behaviour, plus the output folder, end sound, theme and pop-out appearance will return to their defaults. Your Pro purchases are kept.",
            "Reset",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        CancelEndSoundOperations();
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

    partial void OnSelectedEndSoundIdChanged(string value)
    {
        if (_isLoading || string.IsNullOrEmpty(value))
        {
            return;
        }

        _settings.EndSound = value;
        IsEndSoundStatusOpen = false;
        OnPropertyChanged(nameof(CustomEndSoundStatus));
    }

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
