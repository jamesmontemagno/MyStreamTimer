using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyStreamTimer.Core.Settings;
using MyStreamTimer.Core.Timers;

namespace MyStreamTimer.WinUI.ViewModels;

/// <summary>One selectable Segoe Fluent icon offered in the timer icon picker.</summary>
public sealed record IconChoice(string Glyph, string Label);

/// <summary>
/// Settings row for one timer's display name and icon. Writes straight through to <see cref="TimerSettings"/>
/// and notifies the rest of the app (sidebar, timer page, pop-outs) via a callback after every change.
/// </summary>
public sealed partial class TimerAppearanceItem : ObservableObject
{
    public const int MaxNameLength = 40;

    private static readonly IReadOnlyList<IconChoice> SharedIconChoices =
        TimerKindExtensions.IconChoices.Select(choice => new IconChoice(choice.Glyph, choice.Label)).ToList();

    private readonly TimerSettings _settings;
    private readonly Action<TimerKind> _notifyChanged;

    public TimerAppearanceItem(TimerSettings settings, Action<TimerKind> notifyChanged)
    {
        _settings = settings;
        _notifyChanged = notifyChanged;
        Kind = settings.Kind;
    }

    public TimerKind Kind { get; }

    public string DefaultTitle => Kind.Title();

    public string FileName => _settings.FileName;

    public string Description => $"{DefaultTitle} · {FileName}";

    public string EffectiveTitle => _settings.EffectiveTitle;

    public string EffectiveGlyph => _settings.EffectiveIconGlyph;

    public IReadOnlyList<IconChoice> IconChoices => SharedIconChoices;

    public string NameAutomationId => $"TimerName_{Kind.Id()}";

    public string IconAutomationId => $"TimerIcon_{Kind.Id()}";

    public string ResetAutomationId => $"TimerAppearanceReset_{Kind.Id()}";

    /// <summary>
    /// User-typed display name (empty = default). Stored trimmed and capped at <see cref="MaxNameLength"/>; the
    /// property itself is not re-published while typing so the text box keeps trailing spaces and the caret.
    /// </summary>
    public string Name
    {
        get => _settings.DisplayName;
        set
        {
            var name = (value ?? string.Empty).Trim();
            if (name.Length > MaxNameLength)
            {
                name = name[..MaxNameLength];
            }

            if (name == _settings.DisplayName)
            {
                return;
            }

            _settings.DisplayName = name;
            OnPropertyChanged(nameof(EffectiveTitle));
            _notifyChanged(Kind);
        }
    }

    /// <summary>Chosen glyph (empty = the kind's default).</summary>
    public string Glyph
    {
        get => _settings.IconGlyph;
        set
        {
            var glyph = value ?? string.Empty;
            if (glyph == _settings.IconGlyph)
            {
                return;
            }

            _settings.IconGlyph = glyph;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveGlyph));
            _notifyChanged(Kind);
        }
    }

    /// <summary>The icon choice matching <see cref="EffectiveGlyph"/>, used to pre-select the picker.</summary>
    public IconChoice? CurrentChoice =>
        SharedIconChoices.FirstOrDefault(choice => choice.Glyph == EffectiveGlyph);

    [RelayCommand]
    private void Reset()
    {
        _settings.DisplayName = string.Empty;
        _settings.IconGlyph = string.Empty;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(EffectiveTitle));
        OnPropertyChanged(nameof(EffectiveGlyph));
        _notifyChanged(Kind);
    }
}
