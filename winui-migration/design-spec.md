# My Stream Timer 3.0 — WinUI 3 design spec

**Silhouette:** Settings / developer-tool hybrid → `NavigationView` (Left, compact-collapsible) + card content. Reference: Windows Settings, Dev Home. Hero-style timer preview inside each timer page.

## Window
- Main: default **940 × 620** DIP (fits two dashboard columns with the nav pane open), centred on the work area and capped to 85 % of it; minimum **640 × 480** (legacy min). Restored bounds are clamped to the current display. Mica backdrop (`MicaBackdrop`, fallback `DesktopAcrylicBackdrop`), `ExtendsContentIntoTitleBar`, `TitleBar` control with app icon + "My Stream Timer" + (optional) running-timer pill.
- Pop-out (per timer): default **400 × 160**, no title bar/border (`OverlappedPresenter.SetBorderAndTitleBar(false,false)`), `IsAlwaysOnTop` follows *Stay on top*, drag by content, remembers bounds, ESC/double-click closes, right-click `MenuFlyout` (Close, Reset size/position, Copy file path).
- Responsive: `< 720` px → `PaneDisplayMode=LeftCompact`; timer page two-column sections collapse to one column `< 640`.

## Navigation (sidebar)
```
[Header] COUNTDOWNS      ⏱ Countdown 1 · Countdown 2 · Countdown 3 · Countdown 4 (Pro 🔒)
[Header] COUNT UP        ⬆ Count Up 1 · Count Up 2 (Pro 🔒)
[Header] CLOCK           🕒 Current Time (Pro 🔒)
[Header] MORE            ⚡ Automation · ⭐ Pro
[Footer]                 ℹ About · ⚙ Settings (built-in settings item)
```
- Icons: Segoe Fluent Icons `FontIcon` — Countdown `&#xE916;` (Stopwatch), Count up `&#xE74A;` (Up), Clock `&#xE823;` (Recent), Automation `&#xE945;` (LightningBolt), Pro `&#xE735;` (FavoriteStar), About `&#xE946;` (Info).
- Running timer → small accent dot `InfoBadge` on its nav item. Pro-locked → `&#xE72E;` lock glyph in a `NavigationViewItem.InfoBadge`/trailing icon + opens the Pro upsell `TeachingTip` when clicked (page still shows locked state).
- Persist `LastSelectedPage`.

## Type ramp & spacing
- Page title: `TitleTextBlockStyle` (28/36 semibold). Section headers: `BodyStrongTextBlockStyle`. Body: `BodyTextBlockStyle`. Captions/help: `CaptionTextBlockStyle` + `TextFillColorSecondaryBrush`.
- Hero preview text: 48–64 DIP `FontWeight=SemiBold`, `FontFamily="Cascadia Mono, Consolas, Segoe UI"`, `Typography.NumeralAlignment=Tabular` in Light/Dark; animates with `ContentTransition`-like opacity fade (Implicit `Visibility`/`Opacity` animations).
- Page padding 36 (left/right) / 24 top; section spacing 24; card spacing 4 (SettingsCard stack); inner control spacing 8/12.
- Max content width 1000, left-aligned.

## Brushes (semantic, `{ThemeResource}` at usage sites)
| Purpose | Resource |
|---|---|
| Card surface | `CardBackgroundFillColorDefaultBrush` + `CardStrokeColorDefaultBrush`, `CornerRadius={StaticResource OverlayCornerRadius}` (8) |
| Hero card when running | `AccentFillColorDefaultBrush` tinted border (`AccentFillColorDefaultBrush` 1px) + subtle `ThemeShadow`; idle → default card |
| Finished state | `SystemFillColorSuccessBrush` text / `SystemFillColorSuccessBackgroundBrush` badge |
| Error (write failures) | `InfoBar Severity=Error` |
| Pro badge | `AccentFillColorDefaultBrush` pill with `TextOnAccentFillColorPrimaryBrush` |
| Secondary text | `TextFillColorSecondaryBrush` |
- Accent: leave system accent by default. Brand colour `#169dcf` only for the About header illustration, never as UI chrome.
- HighContrast: rely on system brushes; custom `ThemeDictionaries` include `Light`, `Dark`, `HighContrast`.

## Timer page layout (each TimerKind)
1. **Header row**: Title (`TitleTextBlockStyle`) + status pill (Idle / Running / Paused / Finished) + right-aligned `CommandBar` (compact, `DefaultLabelPosition=Right`): *Pop-out* (`&#xE8A7;`), *Open folder* (`&#xE838;`), overflow: *Copy file path*, *Copy URL (start)*.
2. **Hero card**: live text; below: primary actions as big `Button`s in a `StackPanel`: **Start/Stop** (`Style=AccentButtonStyle` when idle; default when running), **Pause/Resume**, **−1 min**, **+1 min**, **Reset**. Disabled states follow legacy (`IsBusy`).
3. **Time** section (countdown only): `SelectorBar` *Duration | Clock time* → Duration: `NumberBox` Minutes (0–1000, SpinButtonPlacementMode=Compact) + `NumberBox` Seconds (0–59); Clock time: `TimePicker` (FinishAtTime). Disabled while running.
4. **Output** section (`SettingsExpander`): Format (`ComboBox` of style options; Pro styles suffixed with "· Pro" and locked → selecting shows `TeachingTip` "Pro feature" and reverts); Custom output `TextBox` with validation `InfoBar`; Finish text (countdown) ; Show AM/PM toggle (clock); File name `TextBox` + trailing "Copy path" `Button` with `Flyout` confirmation ("Path copied").
5. **Behavior** section: `SettingsCard` Auto start (`ToggleSwitch`), Beep at zero (`ToggleSwitch`).
6. Locked state for Pro kinds when not Pro: center illustration glyph `&#xE72E;`, title "Countdown 4 is a Pro feature", body, `AccentButtonStyle` "See Pro options" → navigates to Pro.

Keyboard: Space = Start/Stop, P = Pause/Resume, R = Reset (only when focus is not in a text input), **Ctrl+Shift+1…7** = switch timer (Ctrl+1…4 is reserved by ZoomIt, which streamers use), Ctrl+, = Settings. All interactive elements have `AutomationProperties.Name`.

## Settings page
`SettingsCard`/`SettingsExpander` groups: **Output folder** (path display + Choose / Open / Test access / Use default / Copy; expander lists file names per timer), **Appearance** (Theme `RadioButtons` System/Light/Dark; Stay on top toggle), **Pop-out appearance** (Pro; font size `Slider` 12–200, font family `ComboBox`, text & background colour `DropDownButton` → `Flyout` with `ColorPicker`, live preview card), **Data** (Reset all settings → confirm `ContentDialog`).

## Automation page
Intro + example cards (each: monospace URL + Copy button with flyout), **Command builder** card: Timer `ComboBox`, Action `ComboBox`, value `NumberBox`/`TimePicker` shown contextually, generated URL (read-only `TextBox`, monospace), `Copy` + `Run`. Tips `Expander` for Stream Deck / OBS.

## Pro page
Status `InfoBar`-like banner card (Free / Lifetime tier with tier colour / Subscription until date). Plan cards in `ItemsRepeater` + `UniformGridLayout` (Bronze/Silver/Gold lifetime; Monthly; 6 Months) each with price, billing period, "Buy" `AccentButtonStyle`; owned → check glyph "Unlocked". Buttons: Restore purchases, Manage subscription, Privacy, Terms. `ProgressRing` while busy.

## About page
App icon, name, version (`x:Bind`), OSS blurb, link buttons (GitHub, X/Twitter, YouTube, Blog), Privacy policy, licenses.

## Dialogs / flyouts policy
- `ContentDialog` only for: destructive confirms (Reset all settings), blocking errors (invalid directory), welcome-back sheet.
- Everything else: `Flyout` / `MenuFlyout` / `TeachingTip` / inline `InfoBar`.
- Toast-style confirmations (copied/saved) → `TeachingTip` auto-dismiss 2 s or transient `InfoBar`.


