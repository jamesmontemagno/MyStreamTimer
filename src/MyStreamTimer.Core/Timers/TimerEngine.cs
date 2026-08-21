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
    const int TickMilliseconds = 100;

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
    float bootMins = -1;
    long extraTicksForUp;
    bool firstTime = true;
    int errors;
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

    // ---------------- Commands ----------------

    public void StartStop() => ExecuteStartStop(true);

    public void PauseResume()
    {
        if (IsTime)
            return;

        var prevBusy = IsBusy;
        ExecuteStartStop();

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
    }

    public void Reset()
    {
        if (!IsBusy)
            return;
        ExecuteStartStop(true);
        ExecuteStartStop(true);
    }

    public void AddMinutes(double minutes)
    {
        if (!IsBusy || minutes == 0)
            return;
        lock (locker)
        {
            if (IsDown)
                endTime = endTime.AddMinutes(minutes);
            else
                extraTicksForUp += TimeSpan.FromMinutes(minutes).Ticks;
        }
    }

    /// <summary>Apply a parsed URL command (legacy <c>Init</c>).</summary>
    public void Apply(UrlCommand command)
    {
        switch (command.Action)
        {
            case CommandAction.Start:
                if (IsBusy)
                    ExecuteStartStop();
                bootMins = command.Minutes;
                ExecuteStartStop();
                break;
            case CommandAction.Pause:
                if (IsBusy)
                    PauseResume();
                break;
            case CommandAction.Resume:
                if (!IsBusy)
                    PauseResume();
                break;
            case CommandAction.Reset:
                Reset();
                break;
            case CommandAction.Add when command.Minutes > 0:
                AddMinutesUnconditional(command.Minutes);
                break;
            case CommandAction.Subtract when command.Minutes > 0:
                AddMinutesUnconditional(-command.Minutes);
                break;
            case CommandAction.Stop:
                if (IsBusy)
                    ExecuteStartStop(true);
                break;
        }
    }

    // legacy Init() adjusted end time even when not running
    void AddMinutesUnconditional(double minutes)
    {
        lock (locker)
        {
            if (IsDown)
                endTime = endTime.AddMinutes(minutes);
            else
                extraTicksForUp += TimeSpan.FromMinutes(minutes).Ticks;
        }
    }

    /// <summary>Used for auto-start / boot-from-URL at construction time.</summary>
    public void StartAtBoot(float minutes = -1)
    {
        bootMins = minutes;
        ExecuteStartStop();
    }

    // ---------------- Core start/stop ----------------

    void ExecuteStartStop(bool forceReset = false)
    {
        if (forceReset)
        {
            bootMins = -1;
            extraTicksForUp = 0;
            currentMinutes = 0;
            firstTime = true;
        }

        if (EffectiveOutputStyle == 0 && !IsTime && !OutputFormatter.IsValidTemplate(settings.Output))
        {
            CountdownOutput = OutputFormatter.InvalidFormatMessage;
            return;
        }

        try
        {
            currentFileName = files.PrepareTarget(global.DirectoryPath, settings.FileName);
        }
        catch (Exception ex)
        {
            CountdownOutput = $"INIT: {ex.Message} | Ensure app has access to this directory. Go to Settings to set a valid directory.";
            return;
        }

        var wasBusy = IsBusy;
        if (wasBusy)
            StopLoop();

        State = wasBusy ? TimerState.Idle : TimerState.Running;

        if (forceReset && !IsBusy)
        {
            WriteTimeToDisk(string.Empty);
            CanPauseResume = false;
            CountdownOutput = string.Empty;
        }

        if (IsBusy)
            platform.StartActivity(Kind.Id());
        else
            platform.StopActivity(Kind.Id());

        if (!IsBusy)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        CanPauseResume = !IsTime;

        currentFinished = settings.Finish;
        currentIsDown = IsDown;
        currentShowAmPm = settings.ShowAmPm;
        var currentSeconds = 0;
        if (bootMins > 0)
        {
            currentMinutes = bootMins;
            extraTicksForUp = 0;
            bootMins = -1;
        }
        else if (extraTicksForUp > 0)
        {
            currentMinutes = 0; // resuming a count-up
        }
        else if (settings.UseMinutes)
        {
            currentMinutes = settings.Minutes;
            currentSeconds = settings.Seconds;
        }
        else
        {
            var now = clock.Now;
            var finishAt = settings.FinishAtTime;
            if (finishAt > now.TimeOfDay)
                currentMinutes = (float)(finishAt.TotalMinutes - now.TimeOfDay.TotalMinutes);
            else
            {
                currentMinutes = (float)(1440.0 - now.TimeOfDay.TotalMinutes);
                currentMinutes += (float)finishAt.TotalMinutes;
            }
        }

        currentOutput = OutputFormatter.NormalizeTemplate(settings.Output);
        currentBeepAtZero = settings.MakeSound;
        currentOutputStyle = EffectiveOutputStyle;

        var start = clock.Now;
        if (currentIsDown)
            startTime = start;
        else if (!IsTime)
            startTime = start.AddMinutes(-currentMinutes).AddSeconds(-currentSeconds);

        endTime = start.AddMinutes(currentMinutes).AddSeconds(currentSeconds);
        prevTime = TimeSpan.FromDays(1);
        firstTime = true;

        StateChanged?.Invoke(this, EventArgs.Empty);

        loopCts = new CancellationTokenSource();
        var token = loopCts.Token;
        loopTask = Task.Run(() => UpdateLoop(token), CancellationToken.None);
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
                            _ = platform.BeepAsync();
                        return;
                    }

                    var elapsed = end - now;
                    if (SameSecond(prevTime, elapsed) && !firstTime)
                        goto Delay;
                    firstTime = false;
                    prevTime = elapsed;
                    text = OutputFormatter.FormatElapsed(elapsed, currentOutputStyle, currentOutput);
                }
                else if (IsTime)
                {
                    var minuteRes = OutputFormatter.TimeStyleIsMinuteResolution(currentOutputStyle);
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
            }

        Delay:
            if (token.IsCancellationRequested)
                return;
            try { await Task.Delay(TickMilliseconds, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
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
                return WriteTimeToDisk(text);
            if (errors > 5)
                CountdownOutput = $"{ex.Message} | Ensure app has access to this directory. Go to Settings to set a valid directory.";
            return false;
        }
    }

    public void Dispose()
    {
        StopLoop();
        platform.StopActivity(Kind.Id());
    }
}
