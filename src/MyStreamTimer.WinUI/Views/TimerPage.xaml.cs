using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using MyStreamTimer.WinUI.Services;
using MyStreamTimer.WinUI.ViewModels;

namespace MyStreamTimer.WinUI.Views;

/// <summary>
/// Timer experience for one <see cref="TimerViewModel"/>. A single cached instance is reused for every timer:
/// the shell passes the view model as the navigation parameter and bindings are refreshed on arrival.
/// </summary>
public sealed partial class TimerPage : Page
{
    private static readonly TimeSpan CopiedTipDuration = TimeSpan.FromSeconds(2);

    private readonly DispatcherQueueTimer _copiedTipTimer;
    private TimerViewModel? _viewModel;
    private bool _isSyncingTimeMode;
    private bool _isShadowReceiverWired;

    public TimerPage()
    {
        InitializeComponent();

        _copiedTipTimer = DispatcherQueue.CreateTimer();
        _copiedTipTimer.Interval = CopiedTipDuration;
        _copiedTipTimer.IsRepeating = false;
        _copiedTipTimer.Tick += (_, _) => CopiedTip.IsOpen = false;

        Loaded += OnLoaded;
    }

    /// <summary>The timer currently shown. Never null after the first navigation.</summary>
    public TimerViewModel ViewModel => _viewModel!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is TimerViewModel next && !ReferenceEquals(next, _viewModel))
        {
            Attach(next);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isShadowReceiverWired)
        {
            HeroShadow.Receivers.Add(ShadowReceiver);
            _isShadowReceiverWired = true;
        }

        if (_viewModel is not null)
        {
            UpdateVisualStates(useTransitions: false);
        }
    }

    private void Attach(TimerViewModel next)
    {
        if (_viewModel is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
            previous.Copied -= OnCopied;
            previous.ProRequired -= OnProRequired;
        }

        CopiedTip.IsOpen = false;
        ProTip.IsOpen = false;

        _viewModel = next;
        next.PropertyChanged += OnViewModelPropertyChanged;
        next.Copied += OnCopied;
        next.ProRequired += OnProRequired;

        Bindings.Update();
        SyncTimeModeSelector();
        UpdateVisualStates(useTransitions: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TimerViewModel.IsRunning):
            case nameof(TimerViewModel.IsPaused):
            case nameof(TimerViewModel.IsFinished):
            case nameof(TimerViewModel.IsIdle):
                UpdateVisualStates(useTransitions: true);
                break;
            case nameof(TimerViewModel.UseMinutes):
                SyncTimeModeSelector();
                break;
        }
    }

    private void UpdateVisualStates(bool useTransitions)
    {
        var vm = ViewModel;

        var hero = vm.IsFinished ? "HeroFinished" : vm.IsRunning ? "HeroRunning" : "HeroIdle";
        var pill = vm.IsRunning ? "PillRunning" : vm.IsPaused ? "PillPaused" : vm.IsFinished ? "PillFinished" : "PillIdle";
        var startStop = vm.IsRunning ? "StartStopDefault" : "StartStopAccent";

        VisualStateManager.GoToState(this, hero, useTransitions);
        VisualStateManager.GoToState(this, pill, useTransitions);
        VisualStateManager.GoToState(this, startStop, useTransitions);

        // Elevation (ThemeShadow) cannot be set from a VisualState setter; lift the hero card while running.
        HeroCard.Translation = vm.IsRunning ? new System.Numerics.Vector3(0, 0, 24) : System.Numerics.Vector3.Zero;
    }

    // ---------------- Time mode (Duration | Clock time) ----------------

    private void SyncTimeModeSelector()
    {
        _isSyncingTimeMode = true;
        try
        {
            TimeModeSelector.SelectedItem = ViewModel.UseMinutes ? DurationItem : ClockTimeItem;
        }
        finally
        {
            _isSyncingTimeMode = false;
        }
    }

    private void OnTimeModeSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSyncingTimeMode || _viewModel is null)
        {
            return;
        }

        ViewModel.UseMinutes = ReferenceEquals(sender.SelectedItem, DurationItem);
    }

    // ---------------- Feedback ----------------

    private void OnCopied(object? sender, string message)
    {
        CopiedTip.Title = message;
        CopiedTip.Target = message.StartsWith("Path", StringComparison.Ordinal) && OutputExpander.IsExpanded
            ? CopyPathButton
            : null;
        CopiedTip.IsOpen = true;
        _copiedTipTimer.Stop();
        _copiedTipTimer.Start();
    }

    private void OnProRequired(object? sender, string message)
    {
        ProTip.Subtitle = message;
        ProTip.Target = message.Contains("Pop-out", StringComparison.Ordinal) ? PopOutButton : FormatCombo;
        ProTip.IsOpen = true;
    }

    private void OnSeeProClick(object sender, RoutedEventArgs e) => NavigateToPro();

    private void OnProTipActionClick(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        NavigateToPro();
    }

    private static void NavigateToPro() => NavigationService.Default.NavigateTo(NavigationService.ProTag);

    // ---------------- Keyboard (Space / P / R) ----------------

    private void OnStartStopAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Space must keep activating whichever button/toggle has focus; only act when focus is on inert content.
        if (_viewModel is null || IsTextInputFocused() || IsSpaceActivatedControlFocused())
        {
            return;
        }

        ViewModel.StartStopCommand.Execute(null);
        args.Handled = true;
    }

    private void OnPauseResumeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_viewModel is null || IsTextInputFocused() || !ViewModel.PauseResumeCommand.CanExecute(null))
        {
            return;
        }

        ViewModel.PauseResumeCommand.Execute(null);
        args.Handled = true;
    }

    private void OnResetAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_viewModel is null || IsTextInputFocused() || !ViewModel.ResetCommand.CanExecute(null))
        {
            return;
        }

        ViewModel.ResetCommand.Execute(null);
        args.Handled = true;
    }

    private bool IsTextInputFocused() =>
        FocusManager.GetFocusedElement(XamlRoot) is TextBox or RichEditBox or PasswordBox or NumberBox or AutoSuggestBox or ComboBox or TimePicker;

    private bool IsSpaceActivatedControlFocused() =>
        FocusManager.GetFocusedElement(XamlRoot) is ButtonBase or ToggleSwitch or Selector or SelectorBarItem or Expander;
}
