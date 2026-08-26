using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;
using MyStreamTimer.WinUI.Helpers;
using MyStreamTimer.WinUI.Services;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>Live text + appearance for one pop-out window. Engine events arrive on a background thread and are marshalled.</summary>
public sealed partial class PopOutViewModel : ObservableObject, IDisposable
{
    private const string Placeholder = "—";

    private readonly TimerEngine _engine;
    private readonly GlobalSettings _settings;
    private readonly PopOutService _popOuts;

    public PopOutViewModel(TimerEngine engine, GlobalSettings settings, PopOutService popOuts)
    {
        _engine = engine;
        _settings = settings;
        _popOuts = popOuts;

        Text = string.IsNullOrWhiteSpace(engine.CountdownOutput) ? Placeholder : engine.CountdownOutput;
        ApplyAppearance();

        _engine.TextChanged += OnTextChanged;
        _popOuts.AppearanceChanged += OnAppearanceChanged;
        _popOuts.TimerAppearanceChanged += OnTimerAppearanceChanged;
    }

    public TimerKind Kind => _engine.Kind;

    public string Title => $"{_engine.Settings.EffectiveTitle} — My Stream Timer";

    public string FilePath => Path.Combine(_settings.DirectoryPath, _engine.Settings.FileName);

    public string FolderPath => _settings.DirectoryPath;

    [ObservableProperty]
    public partial string Text { get; set; } = Placeholder;

    [ObservableProperty]
    public partial double FontSize { get; set; } = GlobalSettings.DefaultPopOutFontSize;

    [ObservableProperty]
    public partial FontFamily FontFamily { get; set; } = FontFamily.XamlAutoFontFamily;

    [ObservableProperty]
    public partial Brush Foreground { get; set; } = new SolidColorBrush(Colors.White);

    [ObservableProperty]
    public partial Brush Background { get; set; } = new SolidColorBrush(Colors.Black);

    private void ApplyAppearance()
    {
        FontSize = Math.Clamp(_settings.PopOutFontSize, 12, 200);
        var family = _settings.PopOutFontFamily;
        FontFamily = string.IsNullOrWhiteSpace(family) ? FontFamily.XamlAutoFontFamily : new FontFamily(family);
        Foreground = ColorHex.ToBrush(_settings.PopOutTextColorHex, Colors.White);
        Background = ColorHex.ToBrush(_settings.PopOutBackgroundColorHex, Colors.Black);
    }

    private void OnTextChanged(object? sender, string text)
    {
        App.DispatcherQueue.TryEnqueue(() => Text = string.IsNullOrWhiteSpace(text) ? Placeholder : text);
    }

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        if (App.DispatcherQueue.HasThreadAccess)
        {
            ApplyAppearance();
        }
        else
        {
            App.DispatcherQueue.TryEnqueue(ApplyAppearance);
        }
    }

    private void OnTimerAppearanceChanged(object? sender, TimerKind kind)
    {
        if (kind == Kind)
        {
            OnPropertyChanged(nameof(Title));
        }
    }

    public void Dispose()
    {
        _engine.TextChanged -= OnTextChanged;
        _popOuts.AppearanceChanged -= OnAppearanceChanged;
        _popOuts.TimerAppearanceChanged -= OnTimerAppearanceChanged;
    }
}
