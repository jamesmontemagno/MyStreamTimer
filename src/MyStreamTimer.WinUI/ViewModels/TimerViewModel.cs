using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public TimerViewModel(TimerEngine engine, GlobalSettings global, ProEntitlement pro, ClipboardService clipboard,
        LauncherService launcher, PopOutService popOuts)
    {
        _engine = engine;
        _global = global;
        _pro = pro;
        _clipboard = clipboard;
        _launcher = launcher;
        _popOuts = popOuts;

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
