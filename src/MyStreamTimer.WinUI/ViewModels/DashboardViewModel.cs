using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>
/// Home dashboard: a thin projection over the shell's per-kind <see cref="TimerViewModel"/>s (in
/// <see cref="TimerKindExtensions.All"/> order) plus a live summary and a single bulk action, <see cref="StopAllCommand"/>.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly GlobalSettings _settings;
    private readonly LauncherService _launcher;

    public DashboardViewModel(ShellViewModel shell, GlobalSettings settings, LauncherService launcher)
    {
        _settings = settings;
        _launcher = launcher;
        Timers = TimerKindExtensions.All.Select(kind => shell.Timers[kind]).ToList();

        foreach (var timer in Timers)
        {
            timer.PropertyChanged += OnTimerPropertyChanged;
        }

        RefreshSummary();
    }

    /// <summary>Resolves the remaining dependencies from <see cref="App.Services"/>.</summary>
    public static DashboardViewModel Create(ShellViewModel shell) =>
        new(shell, App.GetService<GlobalSettings>(), App.GetService<LauncherService>());

    public IReadOnlyList<TimerViewModel> Timers { get; }

    /// <summary>Timers currently running or paused (both hold state the user may want to clear).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningTimers))]
    [NotifyCanExecuteChangedFor(nameof(StopAllCommand))]
    public partial int RunningCount { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    public bool HasRunningTimers => RunningCount > 0;

    [RelayCommand(CanExecute = nameof(HasRunningTimers))]
    private void StopAll()
    {
        foreach (var timer in Timers.Where(timer => timer.IsRunning || timer.IsPaused))
        {
            timer.Stop();
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync() => await _launcher.OpenFolderAsync(_settings.DirectoryPath);

    private void OnTimerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimerViewModel.IsRunning) or nameof(TimerViewModel.IsPaused) or null or "")
        {
            RefreshSummary();
        }
    }

    private void RefreshSummary()
    {
        var running = Timers.Count(timer => timer.IsRunning);
        var paused = Timers.Count(timer => timer.IsPaused);

        RunningCount = running + paused;

        if (running == 0 && paused == 0)
        {
            Summary = "All timers are idle — start one below or open a timer to configure it.";
            return;
        }

        var parts = new List<string>(2);
        if (running > 0)
        {
            parts.Add(running == 1 ? "1 timer running" : $"{running} timers running");
        }

        if (paused > 0)
        {
            parts.Add($"{paused} paused");
        }

        Summary = string.Join(" · ", parts);
    }
}


