using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>
/// UI state for one timer. Wraps a <see cref="TimerEngine"/> (whose events arrive on a background thread and are
/// marshalled to the UI thread here) and writes settings straight through to <see cref="TimerSettings"/>.
/// One instance per <see cref="TimerKind"/> lives for the process lifetime so state survives navigation.
/// </summary>
public sealed partial class TimerViewModel : ObservableObject
{
    private const string ProStyleSuffix = " · Pro";
    private const string CountUpFallbackPreview = "00:00:00";

    private readonly TimerEngine _engine;
    private readonly GlobalSettings _global;
    private readonly ProEntitlement _pro;
    private readonly ClipboardService _clipboard;
    private readonly LauncherService _launcher;
    private readonly PopOutService _popOuts;
    private readonly BeepService _beep;
    private CancellationTokenSource _endSoundCancellation = new();

    public TimerViewModel(TimerEngine engine, GlobalSettings global, ProEntitlement pro, ClipboardService clipboard,
        LauncherService launcher, PopOutService popOuts, BeepService beep)
    {
        _engine = engine;
        _global = global;
        _pro = pro;
        _clipboard = clipboard;
        _launcher = launcher;
        _popOuts = popOuts;
        _beep = beep;

        Kind = engine.Kind;
        IsCountdown = Kind.IsCountdown();
        IsCountUp = Kind.IsCountUp();
        IsTime = Kind.IsTime();
        SupportsPauseResume = !IsTime;
        RefreshAppearance();

        OutputStyleOptions = BuildOutputStyleOptions();
        IsLocked = ComputeIsLocked();
        OutputValidationMessage = Validate(engine.Settings.Output);
        RefreshPreview();

        engine.TextChanged += (_, text) => Dispatch(() => ApplyText(text));
        engine.StateChanged += (_, _) => Dispatch(RefreshState);
        engine.Completed += (_, _) => Dispatch(() =>
        {
            IsFinished = true;
            RefreshState();
        });
        pro.Changed += (_, _) => Dispatch(RefreshPro);
        popOuts.TimerAppearanceChanged += (_, kind) =>
        {
            if (kind == Kind)
            {
                Dispatch(RefreshAppearance);
            }
        };
        popOuts.SettingsReset += (_, _) => Dispatch(() =>
        {
            CancelEndSoundOperations();
            IsEndSoundStatusOpen = false;
            // every pass-through property now reads a default; re-bind everything
            OnPropertyChanged(string.Empty);
            OutputValidationMessage = Validate(_engine.Settings.Output);
            RefreshAppearance();
            RefreshPreview();
        });

        ApplyText(engine.CountdownOutput);
        RefreshState();
    }

    /// <summary>Raised after text was copied to the clipboard; argument is a short confirmation ("Path copied").</summary>
    public event EventHandler<string>? Copied;

    /// <summary>Raised when a Pro-only action was attempted without Pro; argument is the upsell message.</summary>
    public event EventHandler<string>? ProRequired;

    public TimerKind Kind { get; }
    public bool IsCountdown { get; }
    public bool IsCountUp { get; }
    public bool IsTime { get; }
    public bool SupportsPauseResume { get; }
    public bool IsNotTime => !IsTime;

    // ---------------- appearance (user-renamable title + icon) ----------------

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string IconGlyph { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LockedTitle { get; set; } = string.Empty;

    // ---------------- live state ----------------

    /// <summary>Text currently written to the file; empty while idle (the hero then shows <see cref="PreviewText"/>).</summary>
    [ObservableProperty]
    public partial string LiveText { get; set; } = string.Empty;

    /// <summary>What the timer will write when started, rendered in the configured format.</summary>
    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Idle";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddMinuteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SubtractMinuteCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial bool IsIdle { get; set; } = true;

    [ObservableProperty]
    public partial bool IsFinished { get; set; }

    [ObservableProperty]
    public partial bool CanEdit { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseResumeCommand))]
    public partial bool CanPauseResume { get; set; }

    [ObservableProperty]
    public partial string StartStopLabel { get; set; } = "Start";

    [ObservableProperty]
    public partial string PauseResumeLabel { get; set; } = "Pause";

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? OutputValidationMessage { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> OutputStyleOptions { get; set; }

    public bool HasError => ErrorMessage is not null;
    public bool HasValidationError => OutputValidationMessage is not null;

    /// <summary>True when the custom template text box applies (non-clock timer rendering with the Custom style).</summary>
    public bool IsCustomFormat => !IsTime && _engine.EffectiveOutputStyle == 0;

    public string FilePath => Path.Combine(_global.DirectoryPath, FileName);

    // ---------------- settings pass-through ----------------

    public double Minutes
    {
        get => _engine.Settings.Minutes;
        set
        {
            var minutes = ClampToInt(value, 0, 1000);
            if (minutes == _engine.Settings.Minutes)
            {
                return;
            }

            _engine.Settings.Minutes = minutes;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public double Seconds
    {
        get => _engine.Settings.Seconds;
        set
        {
            var seconds = ClampToInt(value, 0, 59);
            if (seconds == _engine.Settings.Seconds)
            {
                return;
            }

            _engine.Settings.Seconds = seconds;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public bool UseMinutes
    {
        get => _engine.Settings.UseMinutes;
        set
        {
            if (value == _engine.Settings.UseMinutes)
            {
                return;
            }

            _engine.Settings.UseMinutes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseClockTime));
            RefreshPreview();
        }
    }

    public bool UseClockTime => !UseMinutes;

    public TimeSpan FinishAtTime
    {
        get => _engine.Settings.FinishAtTime;
        set
        {
            if (value == _engine.Settings.FinishAtTime)
            {
                return;
            }

            _engine.Settings.FinishAtTime = value;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string Output
    {
        get => _engine.Settings.Output;
        set
        {
            var output = value ?? string.Empty;
            if (output == _engine.Settings.Output)
            {
                return;
            }

            _engine.Settings.Output = output;
            OutputValidationMessage = Validate(output);
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    public string Finish
    {
        get => _engine.Settings.Finish;
        set
        {
            var finish = value ?? string.Empty;
            if (finish == _engine.Settings.Finish)
            {
                return;
            }

            _engine.Settings.Finish = finish;
            OnPropertyChanged();
        }
    }

    public string FileName
    {
        get => _engine.Settings.FileName;
        set
        {
            var fileName = value ?? string.Empty;
            if (fileName == _engine.Settings.FileName)
            {
                return;
            }

            _engine.Settings.FileName = fileName;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilePath));
        }
    }

    public bool AutoStart
    {
        get => _engine.Settings.AutoStart;
        set
        {
            if (value == _engine.Settings.AutoStart)
            {
                return;
            }

            _engine.Settings.AutoStart = value;
            OnPropertyChanged();
        }
    }

    public bool BeepAtZero
    {
        get => _engine.Settings.MakeSound;
        set
        {
            if (value == _engine.Settings.MakeSound)
            {
                return;
            }

            _engine.Settings.MakeSound = value;
            OnPropertyChanged();
        }
    }

    // ---------------- end sound (per timer) ----------------

    public string SoundToggleHeader => IsCountUp ? "Play sound at time" : "Beep at zero";

    public string SoundToggleDescription => IsCountUp
        ? "Play this timer's sound once when the count up reaches the time below"
        : "Play this timer's sound when the countdown reaches zero";

    public IReadOnlyList<EndSoundChoice> EndSoundChoices => EndSounds.Choices;

    public string SelectedEndSoundId
    {
        get => _engine.Settings.EndSound;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _engine.Settings.EndSound)
            {
                return;
            }

            _engine.Settings.EndSound = value;
            IsEndSoundStatusOpen = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomEndSound));
            OnPropertyChanged(nameof(CustomEndSoundStatus));
        }
    }

    public bool IsCustomEndSound => SelectedEndSoundId == EndSounds.Custom;

    public string CustomEndSoundPath => _engine.Settings.CustomEndSoundPath;

    public string CustomEndSoundStatus => string.IsNullOrWhiteSpace(CustomEndSoundPath)
        ? "No custom audio selected. The default beep will be used."
        : !File.Exists(CustomEndSoundPath)
            ? $"Unavailable: {Path.GetFileName(CustomEndSoundPath)}. The default beep will be used."
            : $"Selected: {Path.GetFileName(CustomEndSoundPath)}";

    public double SoundAtMinutes
    {
        get => _engine.Settings.SoundAtMinutes;
        set
        {
            var minutes = ClampToInt(value, 0, 1000);
            if (minutes == _engine.Settings.SoundAtMinutes)
            {
                return;
            }

            _engine.Settings.SoundAtMinutes = minutes;
            OnPropertyChanged();
        }
    }

    public double SoundAtSeconds
    {
        get => _engine.Settings.SoundAtSeconds;
        set
        {
            var seconds = ClampToInt(value, 0, 59);
            if (seconds == _engine.Settings.SoundAtSeconds)
            {
                return;
            }

            _engine.Settings.SoundAtSeconds = seconds;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEndSoundIdle))]
    public partial bool IsEndSoundBusy { get; set; }

    public bool IsEndSoundIdle => !IsEndSoundBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopEndSoundPreviewCommand))]
    public partial bool IsEndSoundPreviewing { get; set; }

    [ObservableProperty]
    public partial bool IsEndSoundStatusOpen { get; set; }

    [ObservableProperty]
    public partial string EndSoundStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity EndSoundStatusSeverity { get; set; } = InfoBarSeverity.Informational;

    public bool ShowAmPm
    {
        get => _engine.Settings.ShowAmPm;
        set
        {
            if (value == _engine.Settings.ShowAmPm)
            {
                return;
            }

            _engine.Settings.ShowAmPm = value;
            OnPropertyChanged();
            RefreshPreview();
        }
    }

    /// <summary>Index into <see cref="OutputStyleOptions"/>. Setting a Pro style without Pro reverts and raises <see cref="ProRequired"/>.</summary>
    public int OutputStyle
    {
        get => _engine.Settings.OutputStyle;
        set => TrySetOutputStyle(value);
    }

    /// <summary>Returns false (and reverts the bound selector) when the style is Pro-gated and the user is not Pro.</summary>
    public bool TrySetOutputStyle(int value)
    {
        if (value < 0)
        {
            // ComboBox pushes -1 while its ItemsSource is being replaced — ignore and keep the stored value.
            return false;
        }

        if (value == _engine.Settings.OutputStyle)
        {
            return true;
        }

        if (value != 0 && !IsTime && !_pro.IsPro)
        {
            ProRequired?.Invoke(this, "Additional output formats are a Pro feature — head over to the Pro page to upgrade.");
            // Re-publish the stored value on the next dispatcher tick so the two-way binding snaps back.
            App.DispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(OutputStyle)));
            return false;
        }

        _engine.Settings.OutputStyle = value;
        OnPropertyChanged(nameof(OutputStyle));
        OnPropertyChanged(nameof(IsCustomFormat));
        RefreshPreview();
        return true;
    }

    // ---------------- commands ----------------

    [RelayCommand]
    private void StartStop() => _engine.StartStop();

    /// <summary>Forces the timer to Idle (Running or Paused); used by the dashboard's Stop all.</summary>
    public void Stop() => _engine.Stop();

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private void PauseResume() => _engine.PauseResume();

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Reset() => _engine.Reset();

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void AddMinute() => _engine.AddMinutes(1);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void SubtractMinute() => _engine.AddMinutes(-1);

    [RelayCommand]
    private void CopyFilePath()
    {
        _clipboard.SetText(FilePath);
        Copied?.Invoke(this, "Path copied");
    }

    [RelayCommand]
    private void CopyStartUrl()
    {
        var url = UrlCommandParser.Build(Kind, CommandAction.Start, IsTime ? null : Minutes);
        _clipboard.SetText(url);
        Copied?.Invoke(this, "Start URL copied");
    }

    [RelayCommand]
    private async Task OpenFolderAsync() => await _launcher.OpenFolderAsync(_global.DirectoryPath);

    [RelayCommand]
    private void PopOut()
    {
        if (IsLocked || !_pro.IsPro)
        {
            ProRequired?.Invoke(this, "Pop-out timer windows are a Pro feature — head over to the Pro page to upgrade.");
            return;
        }

        _popOuts.Show(Kind);
    }

    [RelayCommand]
    private async Task ChooseEndSoundFileAsync()
    {
        if (IsEndSoundBusy)
        {
            return;
        }

        var cancellationToken = _endSoundCancellation.Token;
        IsEndSoundBusy = true;
        IsEndSoundStatusOpen = false;
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

            _engine.Settings.CustomEndSoundPath = result.Path;
            OnPropertyChanged(nameof(CustomEndSoundPath));
            OnPropertyChanged(nameof(CustomEndSoundStatus));
            SelectedEndSoundId = EndSounds.Custom;
            ShowEndSoundStatus("Custom sound saved. Keep the file in this location so it can be played.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TimerViewModel] Choose end sound failed: {ex.Message}");
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
            var result = await _beep.PreviewAsync(_engine.Settings.EndSoundSelection, cancellationToken);
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
                ShowEndSoundStatus("The sound couldn't be played. Check your audio output and try another file.", InfoBarSeverity.Error);
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

    /// <summary>Stops any preview or pending file validation, e.g. when the page navigates away.</summary>
    public void CancelEndSoundOperations()
    {
        _endSoundCancellation.Cancel();
        _endSoundCancellation.Dispose();
        _endSoundCancellation = new CancellationTokenSource();
        IsEndSoundPreviewing = false;
    }

    private void ShowEndSoundStatus(string message, InfoBarSeverity severity)
    {
        EndSoundStatusMessage = message;
        EndSoundStatusSeverity = severity;
        IsEndSoundStatusOpen = true;
    }

    // ---------------- internals ----------------

    private void RefreshState()
    {
        var state = _engine.State;
        IsRunning = state == TimerState.Running;
        IsPaused = state == TimerState.Paused;
        if (IsRunning)
        {
            IsFinished = false;
        }

        IsIdle = state == TimerState.Idle && !IsFinished;
        CanEdit = !_engine.IsBusy;
        CanPauseResume = SupportsPauseResume && _engine.CanPauseResume;
        StartStopLabel = IsRunning ? "Stop" : "Start";
        PauseResumeLabel = IsPaused ? "Resume" : "Pause";
        StatusText = IsRunning ? "Running" : IsPaused ? "Paused" : IsFinished ? "Finished" : "Idle";
    }

    private void ApplyText(string text)
    {
        if (IsErrorText(text))
        {
            ErrorMessage = text;
            LiveText = string.Empty;
        }
        else
        {
            ErrorMessage = null;
            LiveText = text ?? string.Empty;
        }

        if (string.IsNullOrEmpty(text))
        {
            RefreshPreview();
            if (IsFinished)
            {
                IsFinished = false;
                RefreshState();
            }
        }
    }

    private void RefreshPro()
    {
        IsLocked = ComputeIsLocked();
        OutputStyleOptions = BuildOutputStyleOptions();
        OnPropertyChanged(nameof(OutputStyle));
        OnPropertyChanged(nameof(IsCustomFormat));
        RefreshPreview();
    }

    private void RefreshAppearance()
    {
        Title = _engine.Settings.EffectiveTitle;
        IconGlyph = _engine.Settings.EffectiveIconGlyph;
        LockedTitle = $"{Title} is a Pro feature";
    }

    /// <summary>Renders what the first write would look like with the current duration and format.</summary>
    private void RefreshPreview()
    {
        var style = _engine.EffectiveOutputStyle;
        try
        {
            if (IsTime)
            {
                PreviewText = OutputFormatter.FormatTime(DateTime.Now, style, ShowAmPm);
                return;
            }

            var span = IsCountUp ? TimeSpan.Zero : UseMinutes ? TimeSpan.FromMinutes(Minutes) + TimeSpan.FromSeconds(Seconds) : RemainingUntil(FinishAtTime);
            PreviewText = OutputFormatter.FormatElapsed(span, style, Output);
        }
        catch (FormatException)
        {
            PreviewText = IsCountUp ? CountUpFallbackPreview : OutputFormatter.FormatElapsed(TimeSpan.Zero, 1, string.Empty);
        }
    }

    private static TimeSpan RemainingUntil(TimeSpan timeOfDay)
    {
        var remaining = timeOfDay - DateTime.Now.TimeOfDay;
        if (remaining < TimeSpan.Zero)
        {
            remaining += TimeSpan.FromDays(1);
        }

        return TimeSpan.FromSeconds(Math.Floor(remaining.TotalSeconds));
    }

    private bool ComputeIsLocked() => Kind.RequiresPro() && !_pro.IsPro;

    private IReadOnlyList<string> BuildOutputStyleOptions()
    {
        var options = Kind.OutputStyleOptions();
        if (IsTime || _pro.IsPro)
        {
            return options;
        }

        return options.Select((name, index) => index == 0 ? name : name + ProStyleSuffix).ToList();
    }

    private static string? Validate(string template) =>
        OutputFormatter.IsValidTemplate(template)
            ? null
            : @"This format can't be rendered. Use a .NET TimeSpan template such as {0:hh\:mm\:ss}.";

    private static bool IsErrorText(string text) =>
        text == OutputFormatter.InvalidFormatMessage
        || text.StartsWith("INIT:", StringComparison.Ordinal)
        || text.Contains("Ensure app has access", StringComparison.Ordinal);

    private static int ClampToInt(double value, int min, int max) =>
        double.IsNaN(value) ? min : (int)Math.Clamp(Math.Round(value), min, max);

    private static void Dispatch(Action action)
    {
        if (App.DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            App.DispatcherQueue.TryEnqueue(() => action());
        }
    }
}

