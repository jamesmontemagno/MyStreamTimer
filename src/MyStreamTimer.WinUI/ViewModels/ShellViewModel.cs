using CommunityToolkit.Mvvm.ComponentModel;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>
/// Owns the per-kind <see cref="TimerViewModel"/>s (created once so auto-start / URL-boot timers have UI state
/// before they are ever navigated to) and the persisted sidebar selection.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly GlobalSettings _settings;
    private readonly Dictionary<TimerKind, TimerViewModel> _timers;

    public ShellViewModel(TimerHost host, GlobalSettings settings, ProEntitlement pro, ClipboardService clipboard, LauncherService launcher, PopOutService popOuts)
    {
        _settings = settings;
        _timers = TimerKindExtensions.All.ToDictionary(kind => kind, kind => new TimerViewModel(host.Engine(kind), settings, pro, clipboard, launcher, popOuts));
        host.CommandDispatched += (_, kind) => TimerCommandDispatched?.Invoke(this, kind);
    }

    /// <summary>Resolves all dependencies from <see cref="App.Services"/>.</summary>
    public static ShellViewModel Create() => new(
        App.GetService<TimerHost>(),
        App.GetService<GlobalSettings>(),
        App.GetService<ProEntitlement>(),
        App.GetService<ClipboardService>(),
        App.GetService<LauncherService>(),
        App.GetService<PopOutService>());

    /// <summary>Raised on the UI thread after a <c>mystreamtimer://</c> command targeted a timer; the shell selects it.</summary>
    public event EventHandler<TimerKind>? TimerCommandDispatched;

    public IReadOnlyDictionary<TimerKind, TimerViewModel> Timers => _timers;

    public TimerViewModel Countdown1 => _timers[TimerKind.Countdown];
    public TimerViewModel Countdown2 => _timers[TimerKind.Countdown2];
    public TimerViewModel Countdown3 => _timers[TimerKind.Countdown3];
    public TimerViewModel Countdown4 => _timers[TimerKind.Countdown4];
    public TimerViewModel CountUp1 => _timers[TimerKind.Countup];
    public TimerViewModel CountUp2 => _timers[TimerKind.Countup2];
    public TimerViewModel CurrentTime => _timers[TimerKind.Time];

    /// <summary>Persisted sidebar selection (timer id, "automation", "pro", "about" or "settings").</summary>
    public string LastSelectedPage
    {
        get => _settings.LastSelectedPage;
        set
        {
            if (value == _settings.LastSelectedPage)
            {
                return;
            }

            _settings.LastSelectedPage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Page to show at startup: the last selection, unless a timer is already running (auto-start or URL boot)
    /// and the last selection is not that timer — then the running timer wins so the user sees what is live.
    /// </summary>
    public string InitialPageTag
    {
        get
        {
            var tag = string.IsNullOrWhiteSpace(LastSelectedPage) ? NavigationService.CountdownTag : LastSelectedPage;
            if (GetTimer(tag) is { IsRunning: true })
            {
                return tag;
            }

            var running = _timers.Values.FirstOrDefault(timer => timer.IsRunning);
            return running is null ? tag : running.Kind.Id();
        }
    }

    public TimerViewModel? GetTimer(string? tag) =>
        TimerKindExtensions.FromHost(tag) is { } kind ? _timers[kind] : null;
}
