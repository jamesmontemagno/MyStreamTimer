using MyStreamTimer.Core.Services;

namespace MyStreamTimer.WinUI.Services;

/// <summary>
/// The app's <see cref="ITimerPlatform"/>: tracks running timers (first start acquires the keep-awake request,
/// last stop releases it) and routes beeps to <see cref="BeepService"/>. Safe to call from engine background threads.
/// </summary>
public sealed class TimerPlatformService : ITimerPlatform
{
    private readonly PowerService _power;
    private readonly BeepService _beep;
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public TimerPlatformService(PowerService power, BeepService beep)
    {
        _power = power;
        _beep = beep;
    }

    public bool HasRunningTimers
    {
        get
        {
            lock (_gate)
            {
                return _active.Count > 0;
            }
        }
    }

    public void StartActivity(string id)
    {
        bool first;
        lock (_gate)
        {
            first = _active.Add(id) && _active.Count == 1;
        }

        if (first)
        {
            _power.Acquire();
        }
    }

    public void StopActivity(string id)
    {
        bool last;
        lock (_gate)
        {
            last = _active.Remove(id) && _active.Count == 0;
        }

        if (last)
        {
            _power.Release();
        }
    }

    public Task BeepAsync() => _beep.PlayAsync();
}
