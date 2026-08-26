using System.Diagnostics;
using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Purchases;
using MyStreamTimer.Core.Services;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// Owns one <see cref="TimerEngine"/> per <see cref="TimerKind"/> for the process lifetime, auto-starts timers
/// flagged <c>AutoStart</c>, and dispatches <c>mystreamtimer://</c> commands to the right engine.
/// </summary>
public sealed class TimerHost : IDisposable
{
    private readonly Dictionary<TimerKind, TimerEngine> _engines = [];
    private readonly ProEntitlement _pro;

    /// <summary>Raised on the UI thread after a URL command was applied, so the shell can select that timer.</summary>
    public event EventHandler<TimerKind>? CommandDispatched;

    public TimerHost(ISettingsStore store, GlobalSettings global, ProEntitlement pro, IFileOutputService files,
        ITimerPlatform platform, IClock clock)
    {
        _pro = pro;
        foreach (var kind in TimerKindExtensions.All)
        {
            var settings = new TimerSettings(store, kind);
            _engines[kind] = new TimerEngine(settings, global, pro, files, platform, clock);
        }
    }

    public IReadOnlyDictionary<TimerKind, TimerEngine> Engines => _engines;

    public TimerEngine Engine(TimerKind kind) => _engines[kind];

    /// <summary>Starts every timer whose <c>AutoStart</c> flag is set (Pro-gated kinds only when Pro).</summary>
    public void AutoStart()
    {
        foreach (var (kind, engine) in _engines)
        {
            if (engine.Settings.AutoStart && IsAllowed(kind) && !engine.IsBusy)
            {
                Debug.WriteLine($"[TimerHost] Auto-starting {kind.Id()}");
                engine.StartAtBoot();
            }
        }
    }

    /// <summary>Applies a parsed URL command to its timer. Ignored when invalid or Pro-gated without Pro (legacy behaviour).</summary>
    public bool Dispatch(UrlCommand command)
    {
        if (!command.IsValid || command.Kind is not { } kind || !IsAllowed(kind))
        {
            Debug.WriteLine($"[TimerHost] Ignoring command {command.Action} for host '{command.Host}'");
            return false;
        }

        Debug.WriteLine($"[TimerHost] Dispatch {command.Action} ({command.Minutes}) → {kind.Id()}");
        Engine(kind).Apply(command);
        CommandDispatched?.Invoke(this, kind);
        return true;
    }

    private bool IsAllowed(TimerKind kind) => !kind.RequiresPro() || _pro.IsPro;

    public void Dispose()
    {
        foreach (var engine in _engines.Values)
        {
            engine.Dispose();
        }
    }
}
