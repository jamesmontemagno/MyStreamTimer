using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;

namespace MyStreamTimer.Core.Timers;

public enum TimerState { Idle, Running, Paused }

/// <summary>
/// Runtime for one timer. A faithful port of the legacy <c>TimerViewModel</c> start/stop/pause/resume/
/// reset/add logic and its 100 ms render loop (write-to-disk only when the rendered text changes),
/// decoupled from any UI framework. All public members must be called from one thread (the UI thread);
/// events are raised on the loop's thread, so consumers must marshal.
/// </summary>
public sealed class TimerEngine : IDisposable
{
    /// <summary>Upper bound between samples; keeps URL/UI commands responsive even when the next boundary is far away.</summary>
    const int MaxTickMilliseconds = 250;
    /// <summary>Wake slightly after the boundary so truncation lands on the new second.</summary>
    const int BoundaryGuardMilliseconds = 2;

    readonly object locker = new();
    readonly TimerSettings settings;
    readonly GlobalSettings global;
    readonly ProEntitlement pro;
    readonly IFileOutputService files;
    readonly ITimerPlatform platform;
    readonly IClock clock;

    DateTime startTime;
    DateTime endTime;
    bool currentIsDown;
    bool currentShowAmPm;
    float currentMinutes;
    string currentFinished = string.Empty, currentOutput = string.Empty, currentFileName = string.Empty;
    int currentOutputStyle;
    bool currentBeepAtZero;
    EndSoundSelection currentSound = EndSoundSelection.Default;
    TimeSpan currentSoundAt;
    TimeSpan prevUpElapsed;
    bool hasPrevUpElapsed;
    bool countUpSoundPlayed;
    float bootMins = -1;
    long extraTicksForUp;
    bool firstTime = true;
    int errors;
    long lastFaultTimestamp;
    TimeSpan prevTime = TimeSpan.FromDays(1);
    DateTime prevDateTime;
    CancellationTokenSource? loopCts;
    Task? loopTask;
    string countdownOutput = string.Empty;

    public TimerEngine(TimerSettings settings, GlobalSettings global, ProEntitlement pro,
        IFileOutputService files, ITimerPlatform platform, IClock? clock = null)
    {
        this.settings = settings;
        this.global = global;
        this.pro = pro;
        this.files = files;
        this.platform = platform;
        this.clock = clock ?? SystemClock.Instance;
        Kind = settings.Kind;
        IsDown = Kind.IsCountdown();
        IsTime = Kind.IsTime();
        prevDateTime = this.clock.Now;

        files.EnsureFile(global.DirectoryPath, settings.FileName);
    }

    public TimerKind Kind { get; }
    public TimerSettings Settings => settings;
    public bool IsDown { get; }
    public bool IsTime { get; }
    public bool RequiresPro => Kind.RequiresPro();

    public TimerState State { get; private set; } = TimerState.Idle;
    public bool IsBusy => State == TimerState.Running;
    public bool CanPauseResume { get; private set; }

    /// <summary>Last rendered text (or an error message), mirrors legacy <c>CountdownOutput</c>.</summary>
    public string CountdownOutput
    {
        get => countdownOutput;
        private set
        {
            if (countdownOutput == value) return;
            countdownOutput = value;
            TextChanged?.Invoke(this, value);
        }
    }

    /// <summary>Effective output style honouring Pro gating (non-Pro is always Custom), like legacy.</summary>
    public int EffectiveOutputStyle => pro.IsPro ? settings.OutputStyle : 0;

    public event EventHandler<string>? TextChanged;
    public event EventHandler? StateChanged;
    /// <summary>Raised when a countdown reaches zero (after the finish text is written).</summary>
    public event EventHandler? Completed;
    public event EventHandler<Exception>? Faulted;

    // ---------------- Commands ----------------

    public void StartStop() => ExecuteStartStop(true);

    /// <summary>
    /// Forces the timer to Idle regardless of state (Running or Paused), clearing the output file like a
    /// legacy forced stop. No-op when already Idle.
    /// </summary>
    public void Stop()
    {
        lock (locker)
        {
            if (State == TimerState.Running)
            {
                ExecuteStartStop(true);
                return;
            }

            if (State == TimerState.Paused)
            {
                // Paused == engine idle with pending resume data; discard it and clear the file.
                bootMins = -1;
                extraTicksForUp = 0;
                currentMinutes = 0;
                firstTime = true;
                State = TimerState.Idle;
                CanPauseResume = false;
                try
                {
                    currentFileName = files.PrepareTarget(global.DirectoryPath, settings.FileName);
                    WriteTimeToDisk(string.Empty);
                }
                catch
                {
                    // nothing to clear
                }
                CountdownOutput = string.Empty;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Toggle pause/resume, reporting rejected transitions. Clock timers do not support pausing.</summary>
    public CommandResult PauseResume()
    {
        if (IsTime)
            return new(CommandStatus.Ignored, "Clock timers do not support pause or resume.");

        lock (locker)
        {
            var prevBusy = IsBusy;
            var result = ExecuteStartStop();
            if (!result.Applied)
                return result;

            if (prevBusy)
            {
                if (currentIsDown)
                    bootMins = (float)(endTime - clock.Now).TotalMinutes;
                else
                {
                    var elapsed = clock.Now.AddTicks(extraTicksForUp) - startTime;
                    extraTicksForUp = elapsed.Ticks;
                }
                State = TimerState.Paused;
            }

            CanPauseResume = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }
    }

    /// <summary>Restart a running timer with its configured duration; propagate stop or restart rejection.</summary>
    public CommandResult Reset()
    {
        lock (locker)
        {
            if (!IsBusy)
                return new(CommandStatus.Ignored, "Start the timer before resetting it.");
            if (!TryCalculateTiming(-1, 0, out _))
                return InvalidDuration;
            var result = ExecuteStartStop(true);
            return result.Applied ? ExecuteStartStop(true) : result;
        }
    }

    public CommandResult AddMinutes(double minutes)
    {
        if (!IsBusy || minutes == 0)
            return new(CommandStatus.Ignored, "Start the timer before adjusting its time.");
        return AdjustMinutes(minutes);
    }

    /// <summary>Apply a parsed URL command (legacy <c>Init</c>).</summary>
    public CommandResult Apply(UrlCommand command)
    {
        if (!command.IsValid || command.Kind != Kind || !float.IsFinite(command.Minutes))
            return new(CommandStatus.Rejected, "The timer command is invalid.");

        lock (locker)
        {
            switch (command.Action)
            {
                case CommandAction.Start:
                    if (command.Minutes < 0 && command.Minutes != -1)
                        return InvalidDuration;
                    if (!TryCalculateTiming(command.Minutes, extraTicksForUp, out _))
                        return InvalidDuration;
                    if (IsBusy)
                    {
                        var result = ExecuteStartStop();
                        if (!result.Applied)
                            return result;
                    }
                    bootMins = command.Minutes;
                    return ExecuteStartStop();
                case CommandAction.Pause:
                    if (IsTime || IsBusy)
                        return PauseResume();
                    break;
                case CommandAction.Resume:
                    if (IsTime || !IsBusy)
                        return PauseResume();
                    break;
                case CommandAction.Reset:
                    return Reset();
                case CommandAction.Add when command.Minutes > 0:
                    return AdjustMinutes(command.Minutes);
                case CommandAction.Subtract when command.Minutes > 0:
                    return AdjustMinutes(-command.Minutes);
                case CommandAction.Stop:
                    if (IsBusy)
                        return ExecuteStartStop(true);
                    break;
            }
            return new(CommandStatus.Ignored, "The timer is not in a state that accepts this command.");
        }
    }

    static CommandResult InvalidDuration => new(CommandStatus.Rejected, "The duration is outside the supported date range.");

    CommandResult AdjustMinutes(double minutes)
    {
        if (!double.IsFinite(minutes))
            return InvalidDuration;
        lock (locker)
        {
            if (IsDown && endTime == default)
                return new(CommandStatus.Ignored, "Start this countdown before adjusting its time.");
            try
            {
                if (IsDown)
                    endTime = endTime.AddMinutes(minutes);
                else
                {
                    var ticks = checked(extraTicksForUp + TimeSpan.FromMinutes(minutes).Ticks);
                    _ = clock.Now.AddTicks(ticks);
                    extraTicksForUp = ticks;
                }
                return CommandResult.Success;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
            {
                return InvalidDuration;
            }
        }
    }

    /// <summary>Used for auto-start / boot-from-URL at construction time.</summary>
    public CommandResult StartAtBoot(float minutes = -1)
    {
        if (!TryCalculateTiming(minutes, extraTicksForUp, out _))
            return InvalidDuration;
        bootMins = minutes;
        return ExecuteStartStop();
    }

    // ---------------- Core start/stop ----------------

    readonly record struct RunTiming(float Minutes, DateTime Start, DateTime End);

    bool TryCalculateTiming(float minutes, long extraTicks, out RunTiming timing)
    {
        timing = default;
        if (!float.IsFinite(minutes))
            return false;
        try
        {
            var now = clock.Now;
            var effectiveExtraTicks = minutes >= 0 ? 0 : extraTicks;
            var seconds = 0;
            if (minutes < 0)
            {
                if (effectiveExtraTicks > 0)
                    minutes = 0;
                else if (settings.UseMinutes)
                {
                    minutes = settings.Minutes;
                    seconds = settings.Seconds;
                }
                else
                {
                    var finishAt = settings.FinishAtTime;
                    minutes = (float)(finishAt > now.TimeOfDay
                        ? finishAt.TotalMinutes - now.TimeOfDay.TotalMinutes
                        : 1440.0 - now.TimeOfDay.TotalMinutes + finishAt.TotalMinutes);
                }
            }
            _ = now.AddTicks(effectiveExtraTicks);
            var start = !IsDown && !IsTime ? now.AddMinutes(-minutes).AddSeconds(-seconds) : now;
            timing = new(minutes, start, now.AddMinutes(minutes).AddSeconds(seconds));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
        {
            return false;
        }
    }

    CommandResult ExecuteStartStop(bool forceReset = false)
    {
        RunTiming timing = default;
        bool wasBusy;
        lock (locker)
        {
            // Completion may run during target preparation; keep the chosen transition and its timing together.
            wasBusy = IsBusy;
            if (!wasBusy && !TryCalculateTiming(forceReset ? -1 : bootMins, forceReset ? 0 : extraTicksForUp, out timing))
            {
                CountdownOutput = $"INIT: {InvalidDuration.Message}";
                return InvalidDuration;
            }
        }

        if (EffectiveOutputStyle == 0 && !IsTime && !OutputFormatter.IsValidTemplate(settings.Output))
        {
            CountdownOutput = OutputFormatter.InvalidFormatMessage;
            return new(CommandStatus.Rejected, OutputFormatter.InvalidFormatMessage);
        }

        string preparedFileName;
        try
        {
            preparedFileName = files.PrepareTarget(global.DirectoryPath, settings.FileName);
        }
        catch (Exception ex)
        {
            CountdownOutput = $"INIT: {ex.Message} | Ensure app has access to this directory. Go to Settings to set a valid directory.";
            return new(CommandStatus.Rejected, "The output directory could not be accessed. Check Settings.");
        }

        lock (locker)
        {
            StopLoop();
            currentFileName = preparedFileName;
            if (forceReset)
            {
                bootMins = -1;
                extraTicksForUp = 0;
                currentMinutes = 0;
                firstTime = true;
            }

            State = wasBusy ? TimerState.Idle : TimerState.Running;

            var result = CommandResult.Success;
            if (forceReset && wasBusy)
            {
                if (!WriteTimeToDisk(string.Empty))
                    result = new(CommandStatus.Rejected, "The timer stopped, but its output file could not be cleared. Check Settings.");
                CanPauseResume = false;
                CountdownOutput = result.Applied ? string.Empty : $"INIT: {result.Message}";
            }

            if (!wasBusy)
                platform.StartActivity(Kind.Id());
            else
                platform.StopActivity(Kind.Id());

            if (wasBusy)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
                return result;
            }

            CanPauseResume = !IsTime;

            currentFinished = settings.Finish;
            currentIsDown = IsDown;
            currentShowAmPm = settings.ShowAmPm;
            var isResume = bootMins < 0 && extraTicksForUp > 0;
            if (!isResume)
                countUpSoundPlayed = false; // once per run; a resumed count-up keeps its played state
            if (bootMins >= 0)
            {
                extraTicksForUp = 0;
                bootMins = -1;
            }
            currentMinutes = timing.Minutes;

            currentOutput = OutputFormatter.NormalizeTemplate(settings.Output);
            currentBeepAtZero = settings.MakeSound;
            currentSound = settings.EndSoundSelection;
            currentSoundAt = settings.SoundAt;
            hasPrevUpElapsed = false;
            currentOutputStyle = EffectiveOutputStyle;

            startTime = timing.Start;
            endTime = timing.End;
            prevTime = TimeSpan.FromDays(1);
            firstTime = true;

            StateChanged?.Invoke(this, EventArgs.Empty);

            loopCts = new CancellationTokenSource();
            var token = loopCts.Token;
            loopTask = Task.Run(() => UpdateLoop(token), CancellationToken.None);
            return CommandResult.Success;
        }
    }

    void StopLoop()
    {
        loopCts?.Cancel();
        loopCts = null;
        loopTask = null;
    }

    async Task UpdateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var delayMs = MaxTickMilliseconds;
            try
            {
                var now = clock.Now;
                string? text = null;

                if (currentIsDown)
                {
                    DateTime end;
                    lock (locker) end = endTime;

                    if (now >= end)
                    {
                        lock (locker)
                        {
                            if (token.IsCancellationRequested)
                                return;
                            if (now < endTime)
                                continue;
                            text = currentFinished;
                            // stop (not a forceReset — keeps the finish text on screen)
                            State = TimerState.Idle;
                            CanPauseResume = false;
                            platform.StopActivity(Kind.Id());
                            CountdownOutput = text;
                            WriteTimeToDisk(text);
                            StateChanged?.Invoke(this, EventArgs.Empty);
                            Completed?.Invoke(this, EventArgs.Empty);
                            // Beep fire-and-forget so a Start issued during the beep can never race this loop's CTS;
                            // StopLoop() is the only place that clears loopCts.
                            if (currentBeepAtZero)
                                _ = platform.BeepAsync(currentSound);
                        }
                        return;
                    }

                    var elapsed = end - now;
                    delayMs = MillisUntilNextSecondBoundaryDown(elapsed);
                    if (SameSecond(prevTime, elapsed) && !firstTime)
                        goto Delay;
                    firstTime = false;
                    prevTime = elapsed;
                    text = OutputFormatter.FormatElapsed(elapsed, currentOutputStyle, currentOutput);
                }
                else if (IsTime)
                {
                    var minuteRes = OutputFormatter.TimeStyleIsMinuteResolution(currentOutputStyle);
                    delayMs = minuteRes
                        ? (int)(60_000 - (now.TimeOfDay.TotalMilliseconds % 60_000))
                        : 1000 - now.Millisecond;
                    var same = minuteRes
                        ? prevDateTime.Minute == now.Minute && prevDateTime.Hour == now.Hour
                        : prevDateTime.Second == now.Second && prevDateTime.Minute == now.Minute && prevDateTime.Hour == now.Hour;
                    if (same && !firstTime)
                        goto Delay;
                    firstTime = false;
                    prevDateTime = now;
                    text = OutputFormatter.FormatTime(now, currentOutputStyle, currentShowAmPm);
                }
                else
                {
                    TimeSpan elapsed;
                    lock (locker) elapsed = now.AddTicks(extraTicksForUp) - startTime;
                    // The first sample after a start/resume only establishes the baseline, so a count-up that is
                    // already past its target never plays; afterwards it plays the first time the target is crossed.
                    if (currentBeepAtZero && !countUpSoundPlayed && hasPrevUpElapsed && ShouldPlayCountUpSound(prevUpElapsed, elapsed, currentSoundAt))
                    {
                        countUpSoundPlayed = true;
                        _ = platform.BeepAsync(currentSound);
                    }
                    prevUpElapsed = elapsed;
                    hasPrevUpElapsed = true;
                    delayMs = 1000 - (int)(elapsed.TotalMilliseconds % 1000);
                    if (SameSecond(prevTime, elapsed) && !firstTime)
                        goto Delay;
                    firstTime = false;
                    prevTime = elapsed;
                    text = OutputFormatter.FormatElapsed(elapsed, currentOutputStyle, currentOutput);
                }

                if (text is not null && text != CountdownOutput)
                {
                    if (WriteTimeToDisk(text))
                        CountdownOutput = text;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimerEngine[{Kind}] loop error: {ex}");
                ReportFault(ex);
            }

        Delay:
            if (token.IsCancellationRequested)
                return;
            // Sleep until just past the next boundary of the displayed value (never longer than MaxTick), so a second
            // change is never sampled late or skipped — unlike a fixed 100 ms poll subject to timer-resolution jitter.
            var wait = Math.Clamp(delayMs + BoundaryGuardMilliseconds, 1, MaxTickMilliseconds);
            try { await Task.Delay(wait, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>True when a count-up moved from before <paramref name="target"/> to at/after it. A zero target never plays.</summary>
    public static bool ShouldPlayCountUpSound(TimeSpan previous, TimeSpan current, TimeSpan target) =>
        target > TimeSpan.Zero && previous < target && current >= target;

    /// <summary>ms until a counting-down TimeSpan drops to the next whole second (its fractional part).</summary>
    static int MillisUntilNextSecondBoundaryDown(TimeSpan remaining)
    {
        var frac = (int)(remaining.TotalMilliseconds % 1000);
        return frac <= 0 ? 1000 : frac;
    }

    static bool SameSecond(TimeSpan a, TimeSpan b) =>
        a.Seconds == b.Seconds && a.Minutes == b.Minutes && a.Hours == b.Hours && a.Days == b.Days;

    bool WriteTimeToDisk(string text)
    {
        try
        {
            files.Write(currentFileName, text);
            errors = 0;
            return true;
        }
        catch (Exception ex)
        {
            errors++;
            if (errors == 1)
                ReportFault(ex);
            if (errors == 1)
                return WriteTimeToDisk(text);
            if (errors > 5)
                CountdownOutput = $"{ex.Message} | Ensure app has access to this directory. Go to Settings to set a valid directory.";
            return false;
        }
    }

    void ReportFault(Exception exception)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (lastFaultTimestamp != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(lastFaultTimestamp, now) < TimeSpan.FromMinutes(1))
            return;
        lastFaultTimestamp = now;
        Faulted?.Invoke(this, exception);
    }

    public void Dispose()
    {
        StopLoop();
        platform.StopActivity(Kind.Id());
    }
}
