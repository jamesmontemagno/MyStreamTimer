# My Stream Timer for Windows — WinUI 3 rewrite: deep analysis & execution plan

> Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked
> Every phase ends with a **Validation gate**. Do not start the next phase until every gate item is `[x]`.

---

## ▶ Resume here (last updated 2026‑08‑20, branch `winui-rewrite`)

**Where we are:** Phases 0–3 complete and committed. Phases 4 and 5 are implemented (shell, all pages, pop‑outs, welcome‑back, rename/icons) and build clean; their validation gates are **partially** run (screenshots in `winui-migration/screenshots/`, UI smoke script `screenshots/p5-ui-tests.ps1`). Legacy Store app v2.6.2 is installed side‑by‑side with the Dev‑identity build for upgrade testing.

**How to run:** `cd src\MyStreamTimer.WinUI` → `& "$env:USERPROFILE\.copilot\installed-plugins\win-dev-skills\winui\skills\winui-dev-workflow\BuildAndRun.ps1"` (Debug uses `Package.Dev.appxmanifest`, protocol `mystreamtimer-dev://`). Tests: `dotnet test tests\MyStreamTimer.Core.Tests`. Solution: `MyStreamTimer.slnx`.

**Next actions, in order:**
1. Finish **P4‑4 / P5 gates**: run the `winapp ui` accessibility audit on every page, verify dark + high‑contrast screenshots, verify pop‑out stays above OBS with Stay‑on‑top, verify welcome‑back shows once on an upgraded profile (copy legacy `settings.dat` into the Dev package's `Settings\` folder while the app is closed).
2. **P6 Store** (requires Partner Center): create subscription add‑ons `mstsub` / `mstsub6months` (P0‑6), associate the app (`Package.StoreAssociation.xml`), test purchase/restore in sandbox.
3. **P7 Packaging & upgrade test**: Release build `-p:UseDevIdentity=false`, sign with a `CN=Refractored LLC` cert, run `upgrade-test-checklist.md` against the installed 2.6.2.
4. **P8 hardening**: `winui-code-review` + `code-review` agent over `src/`, raise Core coverage to 90 %, perf numbers.
5. **P9**: push branch, confirm `ci.yml` is green, wire secrets for `release.yml` (SIGNING_PFX_BASE64/PASSWORD, STORE_TENANT_ID/SELLER_ID/CLIENT_ID/CLIENT_SECRET, var STORE_FLIGHT_ID), tag `v3.0.0-rc.1`.
6. **P10**: README, remove `legacy/` after 3.0 ships. macOS parity for rename/icons: [#81](https://github.com/jamesmontemagno/MyStreamTimer/issues/81).

**Known gaps / notes:** hotkeys are Ctrl+Shift+1…7 (Ctrl+1…4 belongs to ZoomIt); Time timer URL host is new (`mystreamtimer://time/?start|?stop`); `PublishTrimmed` is off until P8‑5; legacy projects are still at the repo root (P1‑3 move to `legacy/` deferred until the old solution is no longer needed for reference).

---

## 0. Executive summary

The Windows app today is a **UWP (Xamarin.Forms 5) shell** (`MyStreamTimer.UWP`) over shared C# (`MyStreamTimer.Shared`). It will be replaced by a **native WinUI 3 / Windows App SDK desktop app** that:

1. Ships as an **in-place Microsoft Store update of the existing listing** — same package identity (`23875RefractoredLLC.MyStreamTimer`, publisher `CN=Refractored LLC`, product `9n5nxx3wk7k7`), same protocol (`mystreamtimer://`), same add-on IDs (`mstbronze`, `mstsilver`, `mstgold`).
2. Is **100 % backward compatible** with everything a user configured in the Xamarin.Forms version: every `Plugin.Settings` key, every file name, the output folder, Pro unlock flags, and the full `mystreamtimer://` automation grammar (Stream Deck plugin + OBS integrations must keep working untouched).
3. Brings over the **new features from the SwiftUI macOS rewrite** (`MyStreamTimer.MacSwift`): sidebar navigation, themes, pop‑out timer windows with Pro appearance customisation, automation command builder, welcome‑back sheet, richer folder management, +/‑ minute controls, `time` automation host, modern purchase UX.
4. Gets a **beautiful Fluent design** (Mica, NavigationView, proper Light/Dark/High‑Contrast theming, accessibility), automated **tests**, and **CI/CD** that builds, tests, packages, signs, and publishes to the Store.

Why it is safe: packaged apps that keep the same Package Family Name share the same `ApplicationData.Current.LocalSettings` store and the Store supports updating a UWP listing with a desktop (Win32/WinUI 3) MSIX package ([What's supported when migrating from UWP to WinUI 3](https://learn.microsoft.com/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/what-is-supported)). Xamarin support ended 1 May 2024, so this is also the only sustainable path.

---

## 1. Deep analysis

### 1.1 Legacy Windows app inventory (what must survive)

| Area | Current implementation | Source |
|---|---|---|
| Shell | `TabbedPage` with 10 tabs: Down, Down 2, Down 3, Down 4, Up, Up 2, Time, Commands, About, Pro | `MyStreamTimer.UI/MainPage.xaml` |
| Timer engine | `TimerViewModel` — 100 ms loop on a long‑running task, writes file only when rendered text changes; start/stop/pause/resume/reset/add‑minute; boot‑start from URL; `Init(mins, action)` for live URL commands | `MyStreamTimer.Shared/ViewModel/TimerViewModel.cs` |
| Output styles (count down/up) | `0` Custom (`string.Format(output, TimeSpan)`), `1` Auto (d:hh:mm:ss → ss), `2` Total seconds (`N0`), `3` Total `M:ss`; styles 1‑3 Pro‑only | same |
| Output styles (time) | `0` `h:mm`, `1` `h:mm:ss`, `2` `H:mm`, `3` `H:mm:ss`; `ShowAMPM` appends ` tt` | same |
| Finish behaviour | writes `Finish` text, stops, optional beep (3 × 2 kHz WAV generated in memory) | same + `UWP/Services/PlatformHelpers.cs` |
| File output | `File.WriteAllText(Path.Combine(GlobalSettings.DirectoryPath, FileName), text)`; folder created on demand; empty file created on VM init | same |
| Default output folder | `Path.Combine(IPlatformHelpers.BaseDirectory, "MyStreamTimer")` where UWP `BaseDirectory = Environment.GetFolderPath(CommonApplicationData)` → on UWP this resolves through `Windows.Storage.AppDataPaths.GetDefault().ProgramData`, i.e. a **per‑package isolated path**, not `C:\ProgramData` | `UWP/Services/PlatformHelpers.cs`, corefx `Environment.WinRT.cs` |
| Pro gating | `Countdown4`, `Countup2`, `Time` tabs + output styles 1‑3 require `GlobalSettings.IsPro` (`IsBronze || IsSilver || IsGold || (HasTippedSub && IsSubValid)`); DEBUG builds are always Pro | `Settings.cs`, `TimerViewModel.RequiresPro` |
| Purchases (Windows) | `Plugin.InAppBilling` over `Windows.Services.Store`; durables `mstbronze`, `mstsilver`, `mstgold`; restore = `GetPurchasesAsync`; price cache keys `ProPrice`, `ProPriceDate` (7‑day refresh) | `ProViewModel.cs` |
| Protocol activation | `uap:Protocol Name="mystreamtimer"` → `Utils.ParseStartupArgs` → `MainPage.DownVM…Init()`; app is single‑instanced by UWP | `UWP/App.xaml.cs`, `UI/App.xaml.cs`, `Package.appxmanifest` |
| Store review prompt | on 10th launch (`TimesUsed`) via `StoreRequestHelper.SendRequestAsync(…,16,…)` | `UI/MainPage.xaml.cs`, `PlatformHelpers.StoreReview` |
| Keep‑alive | `ExtendedExecutionSession` + "Do not minimize window" red label (UWP suspension workaround — **not needed** in a desktop app) | `UWP/MainPage.xaml.cs` |
| Stay on top | setting exists but hidden on UWP (`OnPlatform UWP=false`) — becomes a real feature on WinUI (`OverlappedPresenter.IsAlwaysOnTop`) | `TabAboutPage.xaml` |
| Window | 640×480 preferred launch size and min size | `UWP/MainPage.xaml.cs` |
| Package | Identity `23875RefractoredLLC.MyStreamTimer` / `CN=Refractored LLC`, version `2.3.1.0`, min OS 17763, bundle x86/x64/arm/arm64, capability `internetClient`, protocol `mystreamtimer`, tile/splash assets, `BackgroundColor #169dcf` | `Package.appxmanifest`, `.csproj` |

### 1.2 How settings are persisted today (the compatibility contract)

`Xam.Plugins.Settings` on UWP writes to **`ApplicationData.Current.LocalSettings.Values` (root container)** with these encodings (verified against `SettingsPlugin/src/Plugin.Settings/Settings.uwp.cs`):

| CLR type | Stored as | Notes |
|---|---|---|
| `bool`, `int`, `long`, `float`, `double`, `string` | native WinRT value | read back with direct cast |
| `DateTime` | **`string`** = `(-UtcTicks).ToString(InvariantCulture)` | negative = UTC; a positive value is legacy local ticks |
| `decimal` | `string` | unused here |

**Global keys** (`GlobalSettings`): `global_directory_path` (string), `TimesUsed` (int), `IsBronze`/`IsSilver`/`IsGold` (bool), `CheckSubStatus` (bool, default true), `SubPrice`/`SubPrice6Months`/`ProPrice` (string), `HasTippedSub` (bool), `ShowSupportPopUp` (bool), `SubExpirationDate`/`ProPriceDate` (DateTime‑as‑string), `StayOnTop` (bool, default true on Windows), `FirstRun` (bool).

**Per‑timer keys** — suffix `_{id}` where `id ∈ {countdown, countdown2, countdown3, countdown4, countup, countup2, time, giveaway}`:
`key_minutes` (int, default 5 / 0 for up / 60 giveaway), `key_seconds` (int, 0), `key_output` (string, `Starting in {0:hh\:mm\:ss}` / `{0:hh\:mm\:ss}` for up), `key_finish` (string, `Let's do this!`), `key_file_name` (string, `{id}.txt`), `key_auto_start` (bool), `make_sound` (bool), `key_show_ampm` (bool), `key_output_style` (int), `UseMinutes` (bool, true), `FinishAtTime` (**long ticks of a TimeSpan**, `-1` = unset → now+15 min).

➡ Because the new app keeps the same package identity, **these values are already there on first launch — no import step**. The only job is to read/write the *same keys with the same encodings*.

### 1.3 New features from the Swift app to bring to Windows

| # | Feature (Swift source) | Windows implementation notes |
|---|---|---|
| N1 | Sidebar shell with grouped sections (Countdowns 1‑4, Count Up 1‑2, Clock, Automation, Settings, Pro, About) — `RootView.swift` | `NavigationView` (Left, grouped `NavigationViewItemHeader`s), Mica backdrop, custom `TitleBar` |
| N2 | Pop‑out timer preview windows (one per timer, hidden title bar, content‑sized, default 400×160, top‑right) — `TimerMiniView.swift`, `WindowManager.swift`; Pro‑gated | Secondary `Window` per `TimerKind`, `AppWindow` with `OverlappedPresenter` (no title bar, `IsAlwaysOnTop` follows StayOnTop), `SetBorderAndTitleBar(false,false)`; reuse existing window if already open |
| N3 | Pop‑out appearance (Pro): font size (default 48), font family (default system), text colour hex (`#FFFFFF`), background colour hex (`#000000`) with live preview — `SettingsView.swift`, `ColorHex.swift` | New keys `PopOutFontSize` (double), `PopOutFontFamily` (string), `PopOutTextColorHex`, `PopOutBackgroundColorHex`; font list from `Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies()` (Win2D) or DirectWrite via CsWin32; `ColorPicker` control |
| N4 | App theme System/Light/Dark — `AppTheme.swift` | Key `AppTheme` (string `system|light|dark`), applied via `FrameworkElement.RequestedTheme` on root + all secondary windows; title bar caption colours updated |
| N5 | Welcome‑back sheet on 2nd+ launch (`HasSeenWelcomeBackV1`) — `WelcomeBackView.swift` | `ContentDialog` with the three highlights + "Learn about Pro" / "Get started" |
| N6 | Automation page with command builder (pick timer, action, value → copy / run) — `AutomationView.swift` | Replaces Commands tab; "Run in app" dispatches through the same in‑proc URL handler |
| N7 | Folder management: Choose Folder, Open in Explorer, Test Access, Use Default, Copy Path, list of all output file names — `SettingsView.swift` | `FolderPicker` (+`InitializeWithWindow`), `Launcher.LaunchFolderPathAsync`; Test = write & delete random file |
| N8 | +1 / ‑1 minute buttons while running; Open folder / Copy path per timer; segmented Duration vs Clock‑time — `SingleTimerView.swift` | `SegmentedControl`/`Selector Bar`, `NumberBox` for minutes/seconds, `TimePicker` |
| N9 | `time` accepted as URL host (Swift `TimerKind(host:)`) | Extend parser: `mystreamtimer://time/?start|?stop` etc. (keep everything else byte‑identical) |
| N10 | Purchase UX: plan cards (Lifetime tiers + Monthly + 6‑Month subscriptions), status banner with expiry, Restore, Manage subscription — `ProView.swift` | Windows 3.0 adds **subscriptions** to match the Mac app: durables `mstbronze/mstsilver/mstgold` **plus** Store subscription add‑ons `mstsub` (1 month) and `mstsub6months` (6 months). Entitlement via `StoreContext.GetAppLicenseAsync().AddOnLicenses` (active subscription licenses have `ExpirationDate`), purchase via `RequestPurchaseAsync`, manage via `ms-windows-store://` subscription management link. Legacy keys `HasTippedSub`/`SubExpirationDate`/`CheckSubStatus` are written so `IsPro` formula stays unchanged |
| N11 | Display names "Countdown 1…4", "Count Up 1‑2", "Current Time" + short titles | Use in sidebar + window titles |
| N12 | Output template leniency: Swift strips `\:` → treat `{0:hh:mm:ss}` and `{0:hh\:mm\:ss}` as equivalent on input | Normalise user input: if template lacks `\:` escapes inside `{0:…}`, insert them before `string.Format` |
| N13 | **(Windows‑first) Rename timers & pick an icon** — per‑timer display name and icon chosen in Settings, shown in sidebar, page header, pop‑out/window titles. macOS parity tracked in [#81](https://github.com/jamesmontemagno/MyStreamTimer/issues/81) | Keys `DisplayName_{id}` (string, empty = default title) and `IconGlyph_{id}` (Segoe Fluent glyph, empty = default). Settings › Timers `SettingsExpander` with one `SettingsCard` per timer: name `TextBox` + icon `DropDownButton` → `Flyout` with `GridView` of `TimerKindExtensions.IconChoices`; "Reset" per card |

### 1.4 Compatibility invariants (non‑negotiable)

| Invariant | Value |
|---|---|
| Package identity | `Name="23875RefractoredLLC.MyStreamTimer" Publisher="CN=Refractored LLC"` — Store‑signed; version must be `> 2.3.1.0` (use `3.0.0.0`) |
| Package type | **MSIX‑packaged** WinUI 3 desktop app (never `WindowsPackageType=None`) — this is what preserves `LocalSettings` and the Store listing |
| Protocol | `mystreamtimer`; hosts `countdown|countdown1|countdown2|countdown3|countdown4|countup|countup1|countup2` (+ new `time`); queries `mins, secs, to, topofhour, addmins, addsecs, subtractmins, subtractsecs, pause, resume, reset, stop` with **identical precedence and semantics** to `Utils.ParseStartupArgs` |
| Single instance | URL activations must be redirected to the running instance (UWP did this implicitly) |
| Settings store | `ApplicationData.Current.LocalSettings` root container, same keys, same encodings (§1.2) |
| Default output folder | Must resolve to the **same physical path** the UWP build used (`AppDataPaths.GetDefault().ProgramData` + `\MyStreamTimer`). Must be verified empirically in Phase 0 (see P0‑4) — if the desktop package resolves differently, detect the legacy folder and persist it into `global_directory_path` so OBS text sources keep working |
| Output files | Same default names `countdown.txt`, `countdown2.txt`, … `time.txt`; same text; write only on change; UTF‑8 (`File.WriteAllText` default = UTF‑8 **without BOM** — keep) |
| Pro rules | `countdown4`, `countup2`, `time`, output styles 1‑3, (new) pop‑out + appearance are Pro; `IsPro` formula unchanged; DEBUG = Pro |
| Add‑on IDs | `mstbronze`, `mstsilver`, `mstgold` (durables) — **new in 3.0:** `mstsub` (monthly), `mstsub6months` (6‑month) subscription add‑ons created in Partner Center with the same IDs as the Mac app |
| Stream Deck plugin | `MyStreamTimer.StreamDeck` unchanged; its URLs must keep working |

### 1.5 Risks & mitigations

| Risk | Mitigation |
|---|---|
| Default folder path differs between UWP and packaged‑desktop process | P0‑4 empirical check + legacy‑folder discovery fallback + Phase 7 upgrade test |
| `DateTime` encoding mismatch → Pro subscription flags misread | Implement `LegacySettings` exactly per §1.2, with unit tests using captured real `settings.dat` values |
| Store rejects UWP→desktop update | Follow [Store packaging requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements): `.msixupload` / `.msixbundle` with x64+arm64, `runFullTrust`, `Windows.Desktop` target family; test with Package Flight first |
| URL grammar drift | Golden xUnit tests generated from the *legacy* `Utils.ParseStartupArgs` (kept in the test project as oracle) |
| Multi‑instance launches from Stream Deck | `AppInstance.FindOrRegisterForKey("main")` + `RedirectActivationToAsync` in `Program.Main` (`DISABLE_XAML_GENERATED_MAIN`) |
| Timer drift / CPU | Use `PeriodicTimer`/`DispatcherQueueTimer` at 100 ms like legacy, compute from wall clock, write only on change (legacy behaviour) |
| IAP in desktop needs HWND | `WinRT.Interop.InitializeWithWindow.Initialize(storeContext, hwnd)` |
| Users with ARM32 devices | Drop ARM32 (no WinAppSDK support) — note in Store release notes |
| Trimming/AOT breaks XAML/WinRT | Start without trimming; enable later behind CI check (`winui-packaging/references/sourcegen-patterns.md`) |

---

## 2. Target architecture

```
MyStreamTimer.sln (new, VS 2022+/dotnet 10)
├── src/
│   ├── MyStreamTimer.Core/            net10.0 class lib — no UI, no WinRT
│   │   ├── Timers/        TimerKind, TimerConfiguration, TimerEngine, OutputFormatter
│   │   ├── Automation/    UrlCommandParser (byte-compatible), CommandAction
│   │   ├── Settings/      ISettingsStore, LegacySettingKeys, GlobalSettings, TimerSettings
│   │   ├── Purchases/     IPurchaseService, ProEntitlement (IsPro formula)
│   │   └── Services/      IFileOutputService, IClock, IPlatformServices
│   └── MyStreamTimer.WinUI/           WinUI 3 packaged app (x64 | arm64), MVVM (CommunityToolkit.Mvvm)
│       ├── Services/      LocalSettingsStore (ApplicationData), StoreService, FolderService,
│       │                  BeepService, ClipboardService, WindowService (stay-on-top, pop-outs),
│       │                  ActivationService (protocol + single instance), PowerService (no-sleep)
│       ├── ViewModels/    Shell, Timer, Automation, Settings, Pro, About, PopOut
│       ├── Views/         ShellPage, TimerPage, AutomationPage, SettingsPage, ProPage, AboutPage,
│       │                  PopOutWindow, WelcomeBackDialog
│       ├── Styles/        Colors (light/dark/HC), Typography, Brushes
│       └── Package.appxmanifest (identity above, protocol, runFullTrust)
├── tests/
│   ├── MyStreamTimer.Core.Tests/      xUnit — parser goldens, formatter goldens, engine, settings encodings
│   └── MyStreamTimer.WinUI.UITests/   winapp ui batch scripts (smoke + a11y audit)
├── legacy/ (move: MyStreamTimer.UWP, MyStreamTimer.UI, MyStreamTimer.Shared, MyStreamTimer.PlatformShared, MyStreamTimer.Mac) — kept read-only until 3.0 ships, then deleted
├── MyStreamTimer.MacSwift/ (unchanged)
├── MyStreamTimer.StreamDeck/ (unchanged)
└── .github/workflows/ ci.yml, release.yml
```

Design principles: MVVM with `x:Bind`, Core has zero Windows dependencies (fully unit‑testable), every platform touch point behind an interface, no `async void` outside event handlers, `DispatcherQueue` for UI marshalling.

---

## 3. Execution plan (check off in order)

### Phase 0 — Baseline, environment and compatibility capture
- [x] P0‑1 Verify toolchain: `dotnet --version` ≥ 10.0.x, `winapp --version` ≥ 0.6, `dotnet new list winui` shows templates, Developer Mode on. (If missing → run `/winui-setup`.)
- [x] P0‑2 (v2.6.2 installed via winget) Install the **current Store build** (9n5nxx3wk7k7) on the dev machine; configure every tab with non‑default values (minutes, seconds, output text with `\:`, finish text, file names, auto‑start, beep, output style, FinishAtTime, AM/PM), run a countdown, and note the path shown by "Copy".
- [x] P0‑3 (eng/SettingsDump + tests/Fixtures/legacy-settings.json) Capture `%LOCALAPPDATA%\Packages\23875RefractoredLLC.MyStreamTimer_*\Settings\settings.dat` (copy it) and dump all keys/values with a small script (PowerShell `Get-AppxPackage` + `Windows.Storage.ApplicationData` via `pwsh` or a throwaway console app with package identity). Store the dump (values only, no PII) under `tests/MyStreamTimer.Core.Tests/Fixtures/legacy-settings.json`.
- [x] P0‑4 **Record the exact legacy default output folder** from P0‑2. Then from a throwaway *packaged WinUI* app with the same identity, print `AppDataPaths.GetDefault().ProgramData`, `ApplicationData.Current.LocalFolder.Path`, `LocalCacheFolder.Path`. Decide: (a) identical → use `AppDataPaths`; (b) different → implement `LegacyFolderLocator` that probes the recorded pattern and persists it to `global_directory_path` on first run. Document result here: **(a) — legacy default = `%LOCALAPPDATA%\Packages\23875RefractoredLLC.MyStreamTimer_jcspp7mzn01xr\LocalState\ProgramData\MyStreamTimer`; new app uses `ApplicationData.Current.LocalFolder\ProgramData\MyStreamTimer` (identical for the same PFN).**
- [x] P0‑5 Freeze the URL grammar: copy `Utils.ParseStartupArgs` verbatim into `tests/.../LegacyOracle/LegacyUtils.cs` for golden tests.
- [ ] P0‑6 Snapshot Partner Center facts: current listing, add‑ons (`mstbronze/mstsilver/mstgold` durables), package flights, age rating, privacy URL (`https://refractored.com/about/`). **Create two subscription add‑ons** `mstsub` (1 month, with free‑trial optional) and `mstsub6months` (6 months) and publish them so `StoreContext` can query them before Phase 6.
- [x] P0‑7 Decide min OS: Windows 10 **1809 (17763)** (Windows App SDK minimum) — matches legacy `TargetPlatformMinVersion`.

**Validation gate P0**
- [x] `legacy-settings.json` committed and contains every key from §1.2 with the observed encodings.
- [x] P0‑4 decision recorded with the literal paths observed.
- [x] Toolchain check output pasted into the PR description.

### Phase 1 — Solution scaffolding & build pipeline skeleton
- [x] P1‑1 `dotnet new winui-mvvm -n MyStreamTimer.WinUI -o src/MyStreamTimer.WinUI` (latest WindowsAppSDK + CommunityToolkit.Mvvm; Mica + TitleBar + Frame navigation come with the template).
- [x] P1‑2 `dotnet new classlib -n MyStreamTimer.Core -o src/MyStreamTimer.Core -f net10.0`; `dotnet new xunit -n MyStreamTimer.Core.Tests -o tests/MyStreamTimer.Core.Tests`; add project references; create `MyStreamTimer.sln` (`dotnet new sln`, `dotnet sln add …`). Keep `MyStreamTimer.All.sln` for legacy until Phase 10.
- [ ] P1‑3 (deferred, see Resume notes) Move legacy projects to `legacy/` (git mv), fix `MyStreamTimer.All.sln` paths. Do **not** delete yet.
- [x] P1‑4 `Directory.Build.props`: `Nullable` enable, `ImplicitUsings`, `LangVersion latest`, `TreatWarningsAsErrors` for Core, analyzers (`Microsoft.WindowsAppSDK.Analyzers` from the winui-dev-workflow skill), `.editorconfig` already present (spaces, `var` everywhere, expression bodies).
- [x] P1‑5 `Package.appxmanifest`: set Identity `23875RefractoredLLC.MyStreamTimer` / `CN=Refractored LLC` / `3.0.0.0`; `DisplayName`/`PublisherDisplayName`/`Description`/`BackgroundColor #169dcf` from legacy; `TargetDeviceFamily Windows.Desktop MinVersion 10.0.17763.0`; capabilities `internetClient` + `rescap:runFullTrust`; `uap:Protocol Name="mystreamtimer"` with display name/logo; copy tile/splash/store assets from `legacy/MyStreamTimer.UWP/Assets` (regenerate missing scales from `Art/Art1024.png`).
- [x] P1‑6 Platforms: `x64;arm64` (+ `x86` optional). Never AnyCPU.
- [x] P1‑7 First build + run via `winui-dev-workflow` `BuildAndRun.ps1` (async). Record PID/launch success.
- [x] P1‑8 `.github/workflows/ci.yml`: on push/PR → `windows-latest`, `actions/setup-dotnet` 10.x, `microsoft/setup-WinAppCli`, `dotnet restore`, `dotnet build -c Release -p:Platform=x64`, `dotnet test tests/MyStreamTimer.Core.Tests`, `winapp cert generate --if-exists skip --quiet`, `winapp package … --cert devcert.pfx --quiet`, upload `.msix` artifact.

**Validation gate P1**
- [x] `dotnet build MyStreamTimer.slnx -c Release -p:Platform=x64` succeeds locally with 0 warnings in Core.
- [x] Template app launches via `BuildAndRun.ps1`, Mica + title bar visible (screenshot in PR).
- [ ] CI green on the PR (not yet pushed) with an `.msix` artifact attached.

### Phase 2 — Core port (compatibility first, no UI)
- [x] P2‑1 `TimerKind` enum + metadata (ids, titles "Countdown 1…", short titles "Down…", `RequiresPro`, defaults, `DefaultFileName = {id}.txt`, output style labels per §1.1/§1.3‑N11). Include `Giveaway` id only for settings‑key compatibility (no UI).
- [x] P2‑2 `LegacySettingKeys` + `ISettingsStore` (Get/Set bool,int,long,double,string; DateTime via the §1.2 string encoding helper `LegacyDateTimeCodec`). `GlobalSettings` and `TimerSettings(id)` ported 1:1 from `Settings.cs` (same keys, same defaults incl. Windows `StayOnTop` default = true, `FinishAtTime` ticks/‑1 sentinel).
- [x] P2‑3 `UrlCommandParser` — port `Utils.ParseStartupArgs` byte‑for‑byte (note quirks: lower‑casing the whole query, `Contains("?mins=")` + `Remove(0,6)` semantics, `to=` parsing via `DateTime.TryParse`, midnight wrap, `topofhour` formula). Add `time` host (N9) **after** legacy hosts so legacy behaviour is unchanged.
- [x] P2‑4 `OutputFormatter` — port all branches: custom `string.Format` with `TimeSpan`, Auto ladder (`ddddd…d`, `hh/h`, `mm/m`, `ss`, `N0`), total seconds, total `M:ss`, time styles 0‑3 with `tt`. Implement N12 template leniency (`{0:hh:mm:ss}` → `{0:hh\:mm\:ss}`) and the legacy invalid‑format message `Invalid time format. Use {0:hh\:mm\:ss}`.
- [x] P2‑5 `TimerEngine` (per kind): states Idle/Running/Paused; `Start(bootMinutes?)`, `Stop`, `PauseResume`, `Reset`, `AddMinutes(±)`, `Apply(UrlCommand)` with the exact `Init()` semantics (Start while running = stop+start; Pause only if running; Resume only if paused; Add/Subtract adjust `endTime` or `extraTicksForUp`); 100 ms tick; change‑detection identical to legacy (`PrevTime` second/minute/hour/day compare; time mode minute vs second cadence); finish → write `Finish`, stop, raise `Completed` (beep in platform layer). Emits `TextChanged`, `WriteFailed` (with legacy error strings), `Completed`. Uses injected `IClock` and `IFileOutputService`.
- [x] P2‑6 `FileOutputService` (Core default impl): ensure directory, create empty file on init (legacy `InitializeFile`), `WriteAllText` only on change, retry‑once then surface error after 5 failures (legacy `errors` logic).
- [x] P2‑7 `ProEntitlement`: `IsPro` formula (+ `#if DEBUG` true), `AddSubTime` helper, price cache rules (7 days).
- [x] P2‑8 Tests (xUnit):
  - Parser goldens: table of ≥40 URLs (every host × every query incl. edge cases `?mins=0`, `?secs=90`, `?to=15:30`, `?to=01:00` past midnight, mixed case, unknown host, junk) comparing `UrlCommandParser` vs `LegacyOracle`.
  - Formatter goldens for each style at boundaries (0 s, 9 s, 10 s, 59 s, 1 min, 9:59, 10 min, 59:59, 1 h, 9 h, 10 h, 1 d, 10 d, 100 d …), time styles with/without AM/PM, template leniency, invalid template.
  - Settings codec: round‑trip + decode the captured `legacy-settings.json` values (bools, ints, `FinishAtTime` long, `SubExpirationDate` negative‑ticks string, positive legacy ticks).
  - Engine: countdown completes with finish text; pause/resume keeps remaining; add/subtract minute; reset restarts; writes only on change (count writes over 3 s ≈ 3‑4); Swift parity cases (finish text written, newer start supersedes older, multi‑token custom template advances each second).
- [x] P2‑9 (342 tests, 81 % line coverage — raise to 90 % in P8) Run `dotnet test` and reach ≥ 90 % line coverage on Core (`coverlet`), enforce in CI.

**Validation gate P2**
- [x] All Core tests green locally and in CI; coverage badge/number recorded.
- [x] Parser golden table has 0 diffs vs `LegacyOracle`.
- [ ] Code review (`code-review` agent) on Core PR: no high‑confidence findings open.

### Phase 3 — Windows platform services
- [x] P3‑1 `LocalSettingsStore : ISettingsStore` over `ApplicationData.Current.LocalSettings.Values` (root container). No prefix/containers.
- [x] P3‑2 `DefaultFolderProvider` per P0‑4 decision (`AppDataPaths.GetDefault().ProgramData` + `MyStreamTimer`, or `LegacyFolderLocator`). Expose `DefaultDirectoryPath` for "Use Default".
- [x] P3‑3 `ActivationService`: `DISABLE_XAML_GENERATED_MAIN` + custom `Program.Main`; `AppInstance.FindOrRegisterForKey("main")`; if not current → `RedirectActivationToAsync` and exit; handle `ExtendedActivationKind.Protocol` at launch and on `Activated` event; parse with `UrlCommandParser`; dispatch to the right timer; bring window to front; select that timer in the sidebar (legacy switched tab). Respect Pro gating for `countdown4`/`countup2`/`time` (legacy ignored URL for Pro timers when not Pro).
- [x] P3‑4 `WindowService`: main window 640×480 default & min size, `OverlappedPresenter.IsAlwaysOnTop` bound to `StayOnTop`, applies to pop‑outs too; remember window size/position (new keys `MainWindowBounds`), `Mica`/`DesktopAcrylic` backdrop fallback.
- [x] P3‑5 `PowerService`: while any timer runs, hold a `Windows.System.Display.DisplayRequest` (or `PowerRequest` via CsWin32 `PowerSetRequest(ExecutionRequired)`) so the system does not sleep; release on stop. Remove the "Do not minimize window" label forever.
- [x] P3‑6 `BeepService`: port the generated WAV (200 amplitude, 2000 Hz, 75 ms, 3 plays 200 ms apart) using `MediaPlayer` + `InMemoryRandomAccessStream` (no `MediaElement` in WinUI 3).
- [x] P3‑7 `ClipboardService` (`Windows.ApplicationModel.DataTransfer.Clipboard`), `LauncherService` (`Launcher.LaunchUriAsync`, `LaunchFolderPathAsync`), `DialogService` (`ContentDialog` with `XamlRoot`).
- [x] P3‑8 `FolderService`: `FolderPicker` + `InitializeWithWindow`; Test Access (write/delete random file; block when timers running with legacy message); keep `global_directory_path`. Add picked folders to `StorageApplicationPermissions.FutureAccessList` (desktop packaged apps get normal FS access, but FAL keeps parity with brokered paths).
- [x] P3‑9 `StoreService` (`Windows.Services.Store.StoreContext` + `InitializeWithWindow`): `GetAssociatedStoreProductsAsync(["Durable","Subscription"])` for `mstbronze/mstsilver/mstgold/mstsub/mstsub6months`, `RequestPurchaseAsync` (subscriptions included), entitlements via `GetAppLicenseAsync().AddOnLicenses` (durables → `IsBronze/IsSilver/IsGold`; active subscription license → `HasTippedSub=true`, `SubExpirationDate=license.ExpirationDate`, `CheckSubStatus=true`), `StoreProduct.Skus[..].SubscriptionInfo` for billing period display, price strings to `ProPrice`/`SubPrice`/`SubPrice6Months`/`ProPriceDate`; re‑check licenses on every launch and on `StoreContext.OfflineLicensesChanged`; "Manage subscription" opens `https://account.microsoft.com/services`; `StoreRequestHelper.SendRequestAsync(ctx,16,"")` for rating prompt on 10th launch; all legacy error messages preserved.
- [x] P3‑10 `VersionService` (`Package.Current.Id.Version`), `ConnectivityService` (`NetworkInformation.GetInternetConnectionProfile()`), `HostThemeService` (apply `AppTheme`).

**Validation gate P3**
- [x] (verified with dev scheme `mystreamtimer-dev://`) Manual: `start mystreamtimer://countdown/?mins=1` from cmd with app closed → app opens, Countdown 1 selected and running, `countdown.txt` updating in the default folder; run again while open → **no second window**, timer restarts.
- [ ] Manual: `?pause`, `?resume`, `?addmins=1`, `?subtractsecs=30`, `?reset`, `?stop`, `?to=HH:MM`, `?topofhour`, `countup/?mins=1` all behave as legacy (checklist in PR).
- [ ] Manual: machine does not sleep during a 20‑min countdown with sleep timeout set to 1 min; sleeps after stop.
- [ ] Manual: beep plays 3 times at zero when enabled.
- [ ] Unit/integration tests for `LocalSettingsStore` run inside a packaged test host (or guarded integration test) reading the P0‑3 settings.dat.

### Phase 4 — Shell, design system and timer experience (Fluent redesign)
- [x] P4‑1 Load `winui-design` skill; produce a short design spec (layout grid, type ramp, spacing 4/8/12/16/24, brushes for Light/Dark/HC, motion) saved as `winui-migration/design-spec.md`. **Design bar: the app must look and feel like a first‑party modern Windows 11 app** — Mica window + extended title bar, layered `CardBackgroundFillColorDefault` cards with 8 px radius and 1 px stroke, `SettingsCard`/`SettingsExpander` (CommunityToolkit) for every setting row, proper type ramp (Display/Title/Body), Fluent icons (`FontIcon` Segoe Fluent Icons), accent colour usage, implicit/connected animations, and **flyout‑driven interactions everywhere** — `Flyout`/`MenuFlyout`/`TeachingTip`/`CommandBarFlyout` for copy‑path, folder actions, output‑style help, Pro upsell, colour/font pickers, pop‑out options and confirmation prompts. Dialogs only for destructive/required decisions; never stacked pages for simple choices.
- [x] P4‑2 `ShellPage`: `NavigationView` (Left, compact‑collapsible), header items: **Countdowns** (1‑4), **Count Up** (1‑2), **Clock** (Current Time), then Automation, Pro; Settings via built‑in settings item; About in footer. Pro‑locked items show a lock glyph. Selected item persisted (`LastSelectedPage`). Mica backdrop, extended title bar with app icon + title.
- [x] P4‑3 `TimerPage` (one `TimerViewModel` per kind, created at startup so auto‑start/URL boot work even if never navigated — legacy behaviour):
  - Hero preview card (large monospaced live text, subtle accent glow when running, finish state styling), Start/Stop, Pause/Resume, ‑1 min / +1 min, Reset, Pop‑out — actions in a `CommandBar`; secondary actions (Copy path, Open folder, Reset size) in `MenuFlyout`s; Pro‑locked actions open a `TeachingTip`/`Flyout` upsell instead of a modal.
  - Settings rows use `SettingsCard`/`SettingsExpander` (CommunityToolkit.WinUI.Controls.SettingsControls) for the Fluent settings look.
  - "Time" section: segmented Duration | Clock time; `NumberBox` minutes (0‑1000) and seconds (0‑59); `TimePicker` for FinishAtTime; disabled while running (legacy `IsNotBusy`).
  - "Output" section: format selector as tiles/radio (Custom, Auto, Total Seconds, Total Minutes:Seconds — Pro badge; Time: the 4 clock styles + Show AM/PM), custom output `TextBox` with inline validation, finish text, file name, `Copy path`, `Open folder`.
  - "Behaviour": Auto start, Beep at zero toggles.
  - Pro lock state (full‑page `InfoBar`/illustration + "Go to Pro") for `countdown4`, `countup2`, `time` when not Pro; non‑Pro selecting style 1‑3 shows legacy dialog "Pro Feature…" and reverts to Custom.
- [~] P4‑4 Accessibility & theming pass: every control has `AutomationProperties.Name`, keyboard shortcuts (Space = Start/Stop, P = Pause, R = Reset, Ctrl+Shift+1…7 switch timers — avoid Ctrl+1…4, owned by ZoomIt), visible focus, 4.5:1 contrast, High Contrast theme resources, text scaling 200 % doesn't clip.
- [x] P4‑5 Window sizing: default 900×640 (new design), min 640×480 (legacy), responsive below 720 px width (compact nav).

**Validation gate P4**
- [ ] Build via `BuildAndRun.ps1` clean; screenshot set (light, dark, high contrast, compact width) attached to PR.
- [ ] `winui-code-review` skill run: no MVVM/x:Bind/theming blockers.
- [ ] `winui-ui-testing` smoke script: launch → each nav item visible → Countdown 1 start/stop toggles text → file updates. Accessibility audit from the harness passes (0 errors).
- [ ] Manual regression of every legacy tab's controls mapped to the new page (checklist table in PR with legacy control → new control).

### Phase 5 — New Swift‑derived features
- [x] P5‑1 **Themes (N4)**: Settings → Theme radio (System/Light/Dark) stored in `AppTheme`; applied to main + pop‑out windows + title bar buttons immediately.
- [x] P5‑2 **Pop‑out windows (N2, Pro)**: `PopOutWindow` per timer (reuse if open), no title bar/border, drag by content, default 400×160 top‑right of the main window's display, remembers per‑timer bounds (`PopOutBounds_{id}`), always‑on‑top follows `StayOnTop`, closes with main window, ESC/double‑click closes, context menu (Close, Reset size).
- [x] P5‑3 **Pop‑out appearance (N3, Pro)**: Settings section with font size `Slider` (12‑200, default 48), font family `ComboBox` (system list), text & background `ColorPicker` (hex persisted `PopOutTextColorHex`/`PopOutBackgroundColorHex`, defaults `#FFFFFF`/`#000000`), live preview; non‑Pro shows lock + upgrade.
- [x] P5‑4 **Welcome back (N5)**: on launch, if `TimesUsed >= 1 && !HasSeenWelcomeBackV1` show dialog (Fresh redesign · Themes · Pop‑out previews) with "Learn about Pro" (navigates) / "Get started"; set flag. Never show on a fresh install.
- [x] P5‑5 **Automation page (N6)**: explanation, copyable examples (legacy four + `?pause`, `?addmins=1`), **command builder** (timer `ComboBox`, action `ComboBox` [Start mins / Start secs / Start at time / Top of hour / Add mins / Add secs / Subtract mins / Subtract secs / Pause / Resume / Reset / Stop], value input, generated URL read‑only, Copy, Run), plus Stream Deck/OBS tips.
- [x] P5‑6 **Settings page (N7)**: Output folder card (path, Choose Folder, Open in Explorer, Test Access, Use Default, Copy Path), list of all output file names with per‑timer copy; Stay on top toggle; Theme; Pop‑out appearance; "Reset all settings" (with confirm).
- [x] P5‑7 **About page**: version (`Package.Current.Id.Version`), OSS note, links (GitHub repo, GitHub, Twitter/X, YouTube, Blog), privacy policy, licenses.
- [x] P5‑8 **Pro page (N10)**: status banner (Pro lifetime unlocked ✓ tier colour bronze/silver/gold, "Pro subscription active until {date}", or "Free"), **five** purchase cards (Bronze/Silver/Gold lifetime + Monthly + 6‑Month subscription) with live prices and billing period, Restore Purchases, Manage Subscription, Privacy Policy, Terms; busy indicator; subscription‑expiry refresh prompt ported from legacy Mac `MainPage.OnAppearing`; all legacy dialogs.
- [x] P5‑9 `time` URL host (N9) wired end‑to‑end; Commands/Automation text updated.
- [ ] P5‑10 Update `ProEntitlement` gating list to include pop‑out features; tests.
- [~] P5‑11 **Rename timers & icons (N13)**: Settings › Timers section (name + icon per timer, reset); `TimerSettings.EffectiveTitle`/`EffectiveIconGlyph` consumed by sidebar items, TimerPage header, pop‑out title and Automation builder timer list; live update via a `TimerAppearanceChanged(TimerKind)` event.

**Validation gate P5**
- [ ] Each N‑feature demoed in a short screen recording (`winapp ui` capture) attached to PR.
- [ ] Pop‑out remains on top of a full‑screen OBS preview window when Stay on top is enabled; hidden when disabled.
- [ ] Theme switch persists across restart; no white flash in dark mode on startup.
- [ ] Welcome‑back appears exactly once on an upgraded profile (P0‑3 settings.dat) and never on a clean install.
- [ ] Core tests updated and green; UI smoke script extended to Settings/Automation/Pro navigation.

### Phase 6 — Store & purchases end‑to‑end
- [ ] P6‑1 Associate the project with the Store app (Partner Center → `Package.StoreAssociation.xml`), keep identity fields exactly as §1.4.
- [ ] P6‑2 Implement `StoreService` flows with the Store test/sandbox: prices for the 3 durables **and 2 subscriptions** render in Pro page; purchase flow works for each; restore sets flags; subscription expiry/renewal/cancellation reflected after relaunch; `IsPro` unlocks Pro timers/styles/pop‑out immediately (property change notifications across all VMs).
- [ ] P6‑3 Legacy flag honouring: with P0‑3 settings.dat containing `IsGold=true`, app is Pro without network.
- [ ] P6‑4 Rating prompt on 10th launch (`TimesUsed`) fires once.

**Validation gate P6**
- [ ] Store sandbox purchase + restore recorded (screenshots).
- [ ] Offline launch with legacy Pro flags → Pro features available.
- [ ] `winui-code-review` focus on Store/async/exception handling passes.

### Phase 7 — Packaging, identity and upgrade testing
- [ ] P7‑1 Load `winui-packaging` skill. Release build: `BuildAndRun.ps1 /p:Configuration=Release -SkipRun` for x64 and arm64.
- [ ] P7‑2 Local dev cert: `winapp cert generate --manifest .` / `winapp cert install ./devcert.pfx` (admin); `winapp package <out> --cert ./devcert.pfx --timestamp http://timestamp.digicert.com` → `.msix`; `Add-AppxPackage`.
- [ ] P7‑3 **Upgrade test (the critical one)**: on a clean VM/machine: install current Store 2.3.1 → configure as in P0‑2 → side‑load the 3.0.0 `.msix` signed with a cert whose subject = `CN=Refractored LLC` (dev cert with matching publisher) **or** use a Store Package Flight → verify: all per‑timer settings present, Pro flag present, `global_directory_path` unchanged, default folder identical, existing `countdown.txt` continues to be the file being written, OBS text source still updates, `mystreamtimer://` still routes to the new app (no "choose an app" prompt).
- [ ] P7‑4 Produce Store upload: `.msixupload` / `.msixbundle` containing x64 + arm64 (MSBuild `/p:AppxBundle=Always;AppxBundlePlatforms="x64|arm64";UapAppxPackageBuildMode=StoreUpload;GenerateAppxPackageOnBuild=true` or `winapp package` + `makeappx bundle`). Validate with **Windows App Certification Kit**.
- [ ] P7‑5 Create a **Package Flight** in Partner Center, submit, install on a second machine from the flight, repeat P7‑3 on a real Store‑installed predecessor.
- [ ] P7‑6 Write Store release notes (WinUI rewrite, new features, ARM32 dropped, min OS 1809).

**Validation gate P7**
- [ ] WACK passes.
- [ ] Upgrade‑test checklist 100 % green on two machines (one fresh Windows 10 1809+/one Windows 11).
- [ ] Flight installs and launches; settings/purchases/paths intact; Stream Deck plugin buttons work unchanged.

### Phase 8 — Quality hardening
- [ ] P8‑1 Full `winui-code-review` + `code-review` agent pass over `src/`; fix all high‑confidence items.
- [ ] P8‑2 `winui-ui-testing`: full batch (nav, each timer start/stop/pause/reset/add, settings persistence after restart, theme, pop‑out open/close, automation builder copy, a11y audit, screenshots).
- [ ] P8‑3 Perf: idle CPU < 1 %, running 7 timers < 3 % on a laptop; memory steady over 2 h (no leak from pop‑outs/timers) — measure with Task Manager/PerfView; startup < 1.5 s warm.
- [ ] P8‑4 Crash hardening: unhandled exception logging (`App.UnhandledException` → local log in `LocalFolder`), defensive file‑write errors surfaced in UI exactly like legacy messages.
- [ ] P8‑5 Optional: trimming/ReadyToRun evaluation per `sourcegen-patterns.md`; only enable if WACK + UI tests still pass.

**Validation gate P8**
- [ ] UI test batch green; report attached.
- [ ] Perf numbers recorded in PR.
- [ ] Zero open high‑severity review findings.

### Phase 9 — CI/CD & publishing
- [ ] P9‑1 `ci.yml` (from P1‑8) finalised: matrix `x64, arm64`; `dotnet test` with coverage gate; `winapp package` dev‑signed artifacts; run UI smoke tests on `windows-latest` where feasible (or mark as manual job).
- [~] P9‑2 (drafted, untested) `release.yml` on tag `v*`: build Release both platforms, bundle, sign with production PFX from secrets (`--timestamp`), upload `.msixbundle`/`.msixupload` + symbols as release assets, create GitHub Release with notes.
- [ ] P9‑3 Store publish job: `microsoft/setup-msstore-cli` + `msstore publish` (or StoreBroker) using Partner Center API credentials in secrets; default to **Package Flight** channel, manual approval environment for production submission.
- [ ] P9‑4 Version bumping: `Package.appxmanifest` version from tag (script), `AppxAutoIncrementPackageRevision` off in CI.
- [ ] P9‑5 Dependabot for NuGet + Actions; branch protection requiring CI.

**Validation gate P9**
- [ ] Tag `v3.0.0-rc.1` produces signed artifacts and a flight submission automatically.
- [ ] Flight install from the pipeline output passes the P7‑3 checklist.

### Phase 10 — Docs, cleanup, launch
- [ ] P10‑1 README: replace App Center link (dead) with GitHub Releases + Store; document new features, automation grammar incl. `time`, troubleshooting (folder access, stay‑on‑top, pop‑outs), build instructions (`dotnet`, `winapp`, `BuildAndRun.ps1`).
- [ ] P10‑2 `winui-migration/` keeps this plan with all boxes checked + `design-spec.md` + `upgrade-test-checklist.md`.
- [ ] P10‑3 Remove `legacy/` Xamarin/UWP projects and `MyStreamTimer.All.sln` once 3.0 is live for one release cycle (keep the Mac Swift and StreamDeck projects).
- [ ] P10‑4 Stream Deck plugin: verify unchanged; optionally retarget to .NET 8 later (out of scope).
- [ ] P10‑5 Production Store submission (manual approval) → monitor Partner Center health/crash reports for 2 weeks; hotfix path = tag + release.yml.

**Validation gate P10 (Definition of Done)**
- [ ] 3.0.0 live in the Store as an update to 9n5nxx3wk7k7.
- [ ] Upgrade telemetry/feedback shows no settings‑loss or folder‑path reports in first two weeks.
- [ ] This document fully checked.

---

## 4. Appendices

### A. URL grammar (must stay identical)
Hosts: `countdown|countdown1|countdown2|countdown3|countdown4|countup|countup1|countup2` (+ new `time`).
Query precedence (first match wins, query lower‑cased): `?mins=` → `?secs=` → `?topofhour` → `?to=` → `?addmins=` → `?addsecs=` → `?subtractmins=` → `?subtractsecs=` → `?pause` → `?resume` → `?reset` → `?stop`. Start requires value > 0; `to=` past current time wraps to next day; `topofhour = 60‑min + (60‑sec)/60 − 1`.

### B. Settings keys (complete) — see §1.2; new keys added by 3.0:
`AppTheme`, `PopOutFontSize`, `PopOutFontFamily`, `PopOutTextColorHex`, `PopOutBackgroundColorHex`, `HasSeenWelcomeBackV1`, `MainWindowBounds`, `PopOutBounds_{id}`, `LastSelectedPage`. All new keys live in the same root container; none collide with legacy names.

### C. Tooling map
| Need | Tool |
|---|---|
| Scaffold/build/run | `dotnet new winui-mvvm`, `winui-dev-workflow` → `BuildAndRun.ps1` (async) |
| Design decisions | `winui-design` skill |
| Review | `winui-code-review` skill, `code-review` agent |
| UI automation | `winui-ui-testing` (`winapp ui`) |
| Package/sign/Store | `winui-packaging` (`winapp package/cert/sign`), WACK, Partner Center, `msstore` CLI |
| Implementation help | `winui:winui-dev` agent for XAML/WinAppSDK tasks |
| Docs | Microsoft Learn: [Migrate UWP → Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/migrate-to-windows-app-sdk-ovw), [App instancing & activation](https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing), [Store in‑app purchases](https://learn.microsoft.com/windows/uwp/monetize/in-app-purchases-and-trials), [Package requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements), [AppDataPaths](https://learn.microsoft.com/uwp/api/windows.storage.appdatapaths) |

### D. Upgrade‑test checklist (run in P7‑3, P7‑5, P9 gate)
1. Legacy 2.3.1 installed & configured (all 7 timers non‑default, Pro flag if available).
2. Install 3.0 package → launches without error, welcome‑back shown once.
3. Every per‑timer value identical to what was set (table).
4. "Copy path" yields the same folder as legacy; `countdown.txt` etc. already exist there.
5. Start Countdown 1 → OBS text source (pointing at the old file) updates.
6. `start mystreamtimer://countdown2/?secs=90` → routes to running instance, Countdown 2 runs.
7. Pro: `IsGold` etc. honoured offline; Pro page shows "unlocked".
8. Stay on top / theme / pop‑outs function; settings persist after restart.
9. Uninstall → reinstall 3.0 → settings gone (expected), no crash.




