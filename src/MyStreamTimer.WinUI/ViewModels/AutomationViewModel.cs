using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using MyStreamTimer.Core.Automation;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>What kind of value an automation action needs.</summary>
public enum CommandValueMode
{
    None,
    Minutes,
    Seconds,
    ClockTime,
    TopOfHour,
}

public sealed record TimerOption(TimerKind Kind, string Title);

public sealed record ActionOption(string Label, CommandAction Action, CommandValueMode Mode);

/// <summary>A copyable example URL shown at the top of the Automation page.</summary>
public sealed class ExampleCommand
{
    public ExampleCommand(string title, string description, string url, IRelayCommand<string?> copyCommand)
    {
        Title = title;
        Description = description;
        Url = url;
        CopyCommand = copyCommand;
    }

    public string Title { get; }

    public string Description { get; }

    public string Url { get; }

    public IRelayCommand<string?> CopyCommand { get; }

    public string AutomationName => $"Copy {Title} URL";
}

/// <summary>Automation page: examples plus a command builder that generates and runs <c>mystreamtimer://</c> URLs.</summary>
public sealed partial class AutomationViewModel : ObservableObject
{
    private static readonly ActionOption[] AllActions =
    [
        new("Start (minutes)", CommandAction.Start, CommandValueMode.Minutes),
        new("Start (seconds)", CommandAction.Start, CommandValueMode.Seconds),
        new("Start at time", CommandAction.Start, CommandValueMode.ClockTime),
        new("Start at top of hour", CommandAction.Start, CommandValueMode.TopOfHour),
        new("Add minutes", CommandAction.Add, CommandValueMode.Minutes),
        new("Add seconds", CommandAction.Add, CommandValueMode.Seconds),
        new("Subtract minutes", CommandAction.Subtract, CommandValueMode.Minutes),
        new("Subtract seconds", CommandAction.Subtract, CommandValueMode.Seconds),
        new("Pause", CommandAction.Pause, CommandValueMode.None),
        new("Resume", CommandAction.Resume, CommandValueMode.None),
        new("Reset", CommandAction.Reset, CommandValueMode.None),
        new("Stop", CommandAction.Stop, CommandValueMode.None),
    ];

    private static readonly ActionOption[] TimeActions =
    [
        new("Start", CommandAction.Start, CommandValueMode.None),
        new("Stop", CommandAction.Stop, CommandValueMode.None),
    ];

    private readonly TimerHost _timers;
    private readonly ClipboardService _clipboard;

    public AutomationViewModel(TimerHost timers, ClipboardService clipboard)
    {
        _timers = timers;
        _clipboard = clipboard;

        Timers = TimerKindExtensions.All.Select(k => new TimerOption(k, _timers.Engine(k).Settings.EffectiveTitle)).ToList();
        Examples =
        [
            new("Start a 15-minute countdown", "Starts Countdown 1 at 15 minutes.", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Start, 15), CopyTextCommand),
            new("Start a 90-second countdown", "Starts Countdown 1 at 1 minute 30 seconds.", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Start, 90, valueIsSeconds: true), CopyTextCommand),
            new("Count down to a clock time", "Counts down until 3:30 PM (24-hour format).", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Start, clockTime: "15:30"), CopyTextCommand),
            new("Count down to the top of the hour", "Handy for \"starting soon\" screens.", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Start, topOfHour: true), CopyTextCommand),
            new("Pause", "Pauses Countdown 1; use ?resume to continue.", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Pause), CopyTextCommand),
            new("Add a minute", "Adds one minute to a running Countdown 1.", UrlCommandParser.Build(TimerKind.Countdown, CommandAction.Add, 1), CopyTextCommand),
        ];

        SelectedTimer = Timers[0];
        var now = DateTime.Now.AddMinutes(15);
        ClockTime = new TimeSpan(now.Hour, now.Minute, 0);
    }

    public IReadOnlyList<TimerOption> Timers { get; }

    public IReadOnlyList<ExampleCommand> Examples { get; }

    public ObservableCollection<ActionOption> Actions { get; } = [];

    [ObservableProperty]
    public partial TimerOption? SelectedTimer { get; set; }

    [ObservableProperty]
    public partial ActionOption? SelectedAction { get; set; }

    [ObservableProperty]
    public partial double Value { get; set; } = 5;

    [ObservableProperty]
    public partial TimeSpan ClockTime { get; set; }

    [ObservableProperty]
    public partial bool IsNumberVisible { get; set; }

    [ObservableProperty]
    public partial bool IsTimeVisible { get; set; }

    [ObservableProperty]
    public partial string NumberHeader { get; set; } = "Minutes";

    [ObservableProperty]
    public partial string GeneratedUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRunStatusOpen { get; set; }

    [ObservableProperty]
    public partial string RunStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity RunStatusSeverity { get; set; } = InfoBarSeverity.Informational;

    partial void OnSelectedTimerChanged(TimerOption? value)
    {
        if (value is null)
        {
            return;
        }

        var previous = SelectedAction;
        Actions.Clear();
        foreach (var action in value.Kind.IsTime() ? TimeActions : AllActions)
        {
            Actions.Add(action);
        }

        SelectedAction = Actions.FirstOrDefault(a => a.Label == previous?.Label) ?? Actions[0];
        Rebuild();
    }

    partial void OnSelectedActionChanged(ActionOption? value)
    {
        var mode = value?.Mode ?? CommandValueMode.None;
        IsNumberVisible = mode is CommandValueMode.Minutes or CommandValueMode.Seconds;
        IsTimeVisible = mode == CommandValueMode.ClockTime;
        NumberHeader = mode == CommandValueMode.Seconds ? "Seconds" : "Minutes";
        Rebuild();
    }

    partial void OnValueChanged(double value) => Rebuild();

    partial void OnClockTimeChanged(TimeSpan value) => Rebuild();

    private void Rebuild()
    {
        if (SelectedTimer is null || SelectedAction is null)
        {
            GeneratedUrl = string.Empty;
            return;
        }

        var kind = SelectedTimer.Kind;
        var number = double.IsNaN(Value) ? 0 : Math.Max(0, Value);
        GeneratedUrl = SelectedAction.Mode switch
        {
            CommandValueMode.Minutes => UrlCommandParser.Build(kind, SelectedAction.Action, number),
            CommandValueMode.Seconds => UrlCommandParser.Build(kind, SelectedAction.Action, number, valueIsSeconds: true),
            CommandValueMode.ClockTime => UrlCommandParser.Build(kind, SelectedAction.Action, clockTime: $"{ClockTime.Hours:00}:{ClockTime.Minutes:00}"),
            CommandValueMode.TopOfHour => UrlCommandParser.Build(kind, SelectedAction.Action, topOfHour: true),
            _ => UrlCommandParser.Build(kind, SelectedAction.Action),
        };
    }

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand]
    private void CopyGenerated() => CopyText(GeneratedUrl);

    [RelayCommand]
    private void Run()
    {
        if (string.IsNullOrEmpty(GeneratedUrl))
        {
            return;
        }

        var command = UrlCommandParser.Parse(GeneratedUrl);
        if (!command.IsValid)
        {
            ShowRunStatus("That URL doesn't do anything — check the value (it must be greater than zero).", InfoBarSeverity.Warning);
            return;
        }

        if (_timers.Dispatch(command))
        {
            ShowRunStatus($"Sent \"{SelectedAction?.Label}\" to {SelectedTimer?.Title}.", InfoBarSeverity.Success);
        }
        else
        {
            var reason = SelectedTimer?.Kind.RequiresPro() == true
                ? $"{SelectedTimer.Title} is a Pro timer. Unlock Pro to control it."
                : "The command was ignored. Make sure the timer is in a state that accepts it.";
            ShowRunStatus(reason, InfoBarSeverity.Error);
        }
    }

    private void ShowRunStatus(string message, InfoBarSeverity severity)
    {
        RunStatusMessage = message;
        RunStatusSeverity = severity;
        IsRunStatusOpen = true;
    }
}
