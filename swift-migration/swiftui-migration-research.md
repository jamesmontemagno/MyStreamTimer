# Deep research: converting `MyStreamTimer.Mac` from Xamarin.Forms to a native SwiftUI macOS app

## Executive Summary

This migration is **very feasible**, but it should be treated as a **full native rewrite with a strict compatibility layer**, not as a UI reskin. The current Mac app is a thin Xamarin.Mac shell over shared timer, purchase, settings, and file-output logic; the real compatibility contract is the combination of **custom URL automation**, **plain-text file output for OBS/SLOBS**, **sandboxed folder/bookmark access**, and **App Store purchase identifiers**.[^1][^2][^3][^4]

Strategically, the rewrite is also timely because Microsoft ended support for Xamarin, including Xamarin.Forms and Xamarin.Mac, on May 1, 2024.[^33]

For a seamless user upgrade, the new Swift app should ship as an **update to the existing Mac App Store app**, keeping the same bundle identifier (`com.refractored.mystreamtimer`), the same URL scheme (`mystreamtimer://`), the same App Sandbox entitlements, and the same in-app purchase product IDs (`mstgold`, `mstsub`, `mstsub6months`). If those identities stay stable, StoreKit can rehydrate entitlements and the app can continue reading the same persisted settings and bookmark data.[^5][^6][^7][^8]

The biggest unavoidable constraint is OS support: the current app declares **macOS 10.10** as its minimum OS, while the pure SwiftUI `App` lifecycle requires **macOS 11+**, and the StoreKit 2 entitlement APIs (`Transaction.currentEntitlements`, `Transaction.updates`, `AppStore.sync()`) require **macOS 12+**. So **100% feature parity is achievable**, but **100% OS-version parity is not** if the end state is a pure SwiftUI app.[^5][^9][^10][^11]

## Architecture/System Overview

### Current app shape

```text
┌───────────────────────────┐
│ Xamarin.Mac AppDelegate   │
│ FormsApplicationDelegate  │
└─────────────┬─────────────┘
              │ launches
              ▼
┌───────────────────────────┐
│ Xamarin.Forms MainPage    │
│ 10 tabs / views           │
└─────────────┬─────────────┘
              │ binds to
              ▼
┌───────────────────────────┐
│ Shared C# view models     │
│ TimerViewModel            │
│ ProViewModel              │
│ GlobalSettings / Utils    │
└───────┬───────────┬───────┘
        │           │
        │           ├──────────────▶ StoreKit / Plugin.InAppBilling
        │
        ├──────────────▶ File output to user-selected folder
        │                (OBS/SLOBS compatibility)
        │
        └──────────────▶ macOS services:
                         NSOpenPanel, NSUserDefaults bookmark,
                         ProcessInfo.beginActivity, NSWindow level
```

The current Mac project mostly exists to host Xamarin.Forms, register `IPlatformHelpers`, handle custom URLs, and bridge to native macOS APIs like folder picking, bookmark persistence, store review prompts, and long-running timer activity management.[^1][^3]

### Recommended target shape

```text
┌──────────────────────────────┐
│ SwiftUI App / WindowGroup    │
│ RootTabView                  │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│ AppState / TimerStore        │
│ Observable models per timer  │
└──────┬───────────┬───────────┘
       │           │
       │           ├────────────▶ PurchaseStore (StoreKit 2)
       │           │              - currentEntitlements
       │           │              - updates listener
       │           │              - explicit restore via sync()
       │
       ├────────────▶ BookmarkStore / SettingsStore
       │              - UserDefaults legacy keys
       │              - security-scoped bookmark under "bookmark"
       │
       ├────────────▶ FileOutputService
       │              - same folder + same filenames + same text contract
       │
       └────────────▶ AppKit bridges
                      - NSOpenPanel
                      - NSWindow.level for Stay on Top
                      - ProcessInfo.beginActivity
                      - NSBeep / review prompt
```

That structure preserves the current external contract while giving you a clean, testable native macOS implementation.[^2][^3][^7][^8]

## What must reach 100% feature parity

### User-visible surface

The current app exposes **10 tabs**: `Down`, `Down 2`, `Down 3`, `Down 4`, `Up`, `Up 2`, `Time`, `Commands`, `About`, and `Pro`.[^12]

The timer tabs support all of the following:

| Feature | Current behavior | Why it matters |
|---|---|---|
| Countdown timers | Minutes/seconds or time-of-day countdown, plus multiple independent countdown slots | Core streamer workflow[^13][^14] |
| Count-up timers | Two separate count-up timers | Feature parity / pro gating[^13][^15] |
| Clock output | Writes live current time with 12h/24h formatting options | Overlay use case[^16] |
| Output formatting | Custom template or preset styles like auto / total seconds / total minutes:seconds | External overlay text must not change unexpectedly[^13][^17] |
| File naming | User-defined output file name per timer | OBS/overlay integrations depend on stable filenames[^13][^17] |
| Runtime controls | Start/stop, pause/resume, add minute, reset | Deep-link and UI parity[^13][^14] |
| Auto start | Each timer can auto-start | Streaming automation[^13][^17] |
| Beep at zero | Native Mac beep on finish | Existing UX contract[^3][^13] |
| Stay on top | Window can pin above other apps | Existing Mac-specific feature[^3][^18] |
| Pro gating | `Countdown4`, `Countup2`, and `Time` are gated behind Pro | Must preserve entitlement logic[^15][^19] |

### Automation and external integrations

This app is not just a GUI timer; it is also a **desktop automation endpoint** for stream tooling. It registers the custom URL scheme `mystreamtimer://`, and the repository documents using it from **OBS/SLOBS**, **Stream Deck**, and the command line.[^5][^20]

The supported URL hosts are:

- `countdown`, `countdown1`, `countdown2`, `countdown3`, `countdown4`
- `countup`, `countup1`, `countup2`[^14]

The supported query actions are:

- `?mins=`
- `?secs=`
- `?to=`
- `?topofhour`
- `?addmins=`
- `?addsecs=`
- `?subtractmins=`
- `?subtractsecs=`
- `?pause`
- `?resume`
- `?reset`
- `?stop`[^14]

The Stream Deck plugin also emits these exact URLs from its actions, which means the Swift rewrite must preserve this grammar exactly to avoid breaking existing button setups.[^21][^22][^23]

### File-output contract

The app writes the live timer text to a **plain text file** with `File.WriteAllText(currentFileName, text)` whenever the rendered output changes. By default, files live in `~/Documents/MyStreamTimer`, but users can override that directory from the About tab.[^2][^17][^24]

This file-based contract is the reason the app works with OBS/SLOBS. If the new app changes the path model, file naming, or write semantics, many existing stream overlays will silently break.[^20][^24]

## Compatibility invariants you should preserve exactly

If the goal is “user upgrades and nothing breaks,” these invariants should be treated as **non-negotiable**:

| Invariant | Current value / behavior | Preserve? |
|---|---|---|
| Bundle identifier | `com.refractored.mystreamtimer` | **Yes**[^5] |
| URL scheme | `mystreamtimer://` | **Yes**[^5] |
| App Sandbox | enabled | **Yes**[^6] |
| Security-scoped bookmark entitlement | enabled | **Yes**[^6][^8] |
| Default save folder | `Documents/MyStreamTimer` | **Yes**[^2][^24] |
| Bookmark storage key | `bookmark` in `NSUserDefaults` | **Yes**[^3] |
| Global setting keys | `global_directory_path`, `StayOnTop`, `TimesUsed`, etc. | **Yes**[^17] |
| Per-timer setting keys | `key_minutes_{id}`, `key_file_name_{id}`, etc. | **Yes**[^17] |
| Product IDs | `mstgold`, `mstsub`, `mstsub6months` | **Yes**[^4] |
| Pro gating rules | `Countdown4`, `Countup2`, `Time` require Pro | **Yes**[^15][^19] |

My recommendation is to preserve these keys even if you introduce a new Codable settings model. Read/write the new model **and** mirror the old keys for at least one major version, which makes the upgrade safe and also gives you rollback safety.[^3][^17]

## Recommended migration approach

### 1. Rewrite the Mac app natively, but freeze the compatibility surface first

Do **not** start by redesigning flows. First lock down the current behavior from the repo:

1. Preserve the 10-tab information architecture.
2. Preserve the URL parser semantics exactly.
3. Preserve the folder/bookmark model and all relevant settings keys.
4. Preserve all product identifiers and Pro gating rules.
5. Preserve the output text behavior and filenames.[^12][^14][^17]

This is the fastest path to “feels identical after upgrade,” because the risky parts are the invisible contracts, not the UI styling.[^2][^14][^20]

### 2. Build the SwiftUI shell with a few explicit AppKit bridges

The new app should be mostly SwiftUI, but not “SwiftUI only.” You still need AppKit interop for:

- `NSOpenPanel` folder selection
- `NSWindow.level` control for Stay on Top
- `NSBeep` / store review prompt
- security-scoped bookmark resolution and access
- possibly window lifecycle tuning[^3][^18][^25]

At the scene layer, SwiftUI’s `App` protocol and `WindowGroup` map well to the current single-window app, and `.onOpenURL` is the correct native hook for the existing custom URL behavior.[^10][^25]

```swift
@main
struct MyStreamTimerApp: App {
    @StateObject private var appState = AppState()

    var body: some Scene {
        WindowGroup {
            RootTabView()
                .environmentObject(appState)
                .onOpenURL { url in
                    appState.handleIncomingURL(url)
                }
        }
        Settings {
            SettingsView()
        }
    }
}
```

That is a clean native replacement for the current `AppDelegate -> OpenUrls -> SendOnAppLinkRequestReceived(...)` chain.[^1][^10]

### 3. Port the timer engine as a compatibility-first core

The highest-value code to port faithfully is:

- `Utils.ParseStartupArgs`
- `GlobalSettings`
- `Settings`
- the `TimerViewModel` start/stop/pause/add/reset logic
- the text-rendering logic for countdown, count-up, and time display[^14][^17][^26]

This port should be validated with parity tests. For example, the Swift implementation should produce the same text as the old app for:

- `Starting in {0:hh\:mm\:ss}`
- “auto” formatting
- total seconds
- total minutes:seconds
- 12h/24h time outputs
- finish text on zero[^13][^16][^26]

One subtle but important detail: the current loop ticks every 100 ms, but it only writes to disk when the displayed text changes. Preserve that behavior so output latency stays familiar without excessive disk churn.[^26]

### 4. Migrate settings and file permissions without asking the user to reconfigure

The current app stores most settings through `Plugin.Settings` and the folder bookmark directly in `NSUserDefaults.StandardUserDefaults["bookmark"]`.[^3][^17]

That means the Swift app should:

1. Launch under the **same bundle identifier**.
2. Read the old values from `UserDefaults.standard`.
3. Attempt to resolve the existing bookmark data under the `bookmark` key.
4. If the bookmark is stale, refresh it; if resolution fails, prompt the user to re-pick the folder.
5. Keep using the same default fallback folder `~/Documents/MyStreamTimer`.[^2][^3][^5][^8][^27]

Use `URL.bookmarkData(options: [.withSecurityScope], ...)` and the matching resolution APIs for the sandboxed folder access. Apple explicitly documents this pattern for App Sandbox file access.[^8]

### Why signing identity matters

Security-scoped bookmarks are tied to the app’s signing identity. If the upgrade changes the effective identity, previously saved bookmarks may stop resolving and the user may need to re-grant folder access. That is why the Mac App Store update should stay under the same app identity and team wherever possible.[^8][^28]

### 5. Move purchases to StoreKit 2, but keep the same products

The current app uses `Plugin.InAppBilling` and checks products like:

- `mstgold`
- `mstsub`
- `mstsub6months`[^4]

On macOS, the app already treats gold lifetime + the two subscriptions as the real Mac purchase surface, and it restores entitlements by querying purchase history and checking the latest transaction dates.[^4][^29]

In the Swift rewrite, the cleanest approach is:

- Use StoreKit 2.
- At launch, iterate `Transaction.currentEntitlements`.
- Start a long-lived `Transaction.updates` task immediately.
- Keep a **Restore Purchases** button, but wire it to `try await AppStore.sync()` and then re-read entitlements.[^7][^9][^30]

```swift
@MainActor
func refreshEntitlements() async {
    for await result in Transaction.currentEntitlements {
        guard case .verified(let transaction) = result else { continue }
        unlock(productID: transaction.productID)
    }
}
```

This is a better model than the current cached subscription heuristics because StoreKit 2 makes verified current entitlements the source of truth while still supporting explicit user-initiated restore flows.[^7][^9][^30]

### Purchase continuity across the rewrite

Apple’s App Store Connect docs state that a single in-app purchase can be used across multiple platform versions of the same app. So as long as the Swift client ships as the same app and keeps the same product IDs, users’ lifetime unlocks and subscriptions can continue to sync.[^7]

### 6. Recreate the current “background behavior” the macOS way

There is an important terminology mismatch here: **macOS does not use iOS-style “Background Modes” for this kind of utility app**. Apple’s Xcode docs explicitly note that the Background Modes capability isn’t available for macOS apps.[^11]

The current app’s actual Mac behavior is:

- keep the app process alive while the window is open
- run long-lived timer work in-process
- call `NSProcessInfo.BeginActivity` to mark the timer as a long-running user-initiated activity and disable idle display sleep
- optionally raise the window to `NSWindowLevel.ScreenSaver` when Stay on Top is enabled[^3][^18]

In Swift, you should reproduce that with `ProcessInfo.processInfo.beginActivity(options:reason:)` and `endActivity(_:)`, which Apple documents for long-running activities.[^31]

### Important nuance

The current app **terminates when the last window closes**. So strict parity does **not** require a permanent menubar utility or login item; those would be product enhancements, not compatibility requirements.[^1]

If you want to improve reliability for streamers who minimize or background the app frequently, an optional `MenuBarExtra` could help on newer macOS versions, but that would be a new feature and should be opt-in because `MenuBarExtra` itself is macOS 13+.[^32]

## Recommended rollout plan

### Option A — best overall: ship the Swift app as a direct in-place App Store update

This is the option I recommend.

1. Build the new SwiftUI app under the **same bundle ID** and app record.
2. Keep the same custom URL scheme and product IDs.
3. Implement a legacy settings adapter that reads the existing defaults keys and bookmark.
4. Validate file output, purchase restore, and deep links on upgrade from the shipping app.
5. Release to TestFlight first, then stage the App Store rollout.[^5][^7][^17]

This gives users the cleanest experience: they update the app, their timers/settings still exist, their purchases still unlock Pro, and their existing Stream Deck / OBS automation keeps working.[^4][^20][^21]

### Option B — lower-risk bridge: ship one “stabilization” release before the rewrite

If you want extra safety, ship one final Xamarin.Mac update first that:

- normalizes all timer settings into the exact legacy keys you intend to keep,
- verifies bookmark health,
- maybe writes a small version marker into defaults for migration diagnostics.[^3][^17]

Then release the Swift update. This reduces migration risk, but it is optional rather than required.

## Recommended technical scope and sequencing

| Phase | Deliverable | Why first |
|---|---|---|
| 1 | Compatibility core: timer parser + settings + output formatting | Lowest UI risk, highest parity value[^14][^17][^26] |
| 2 | SwiftUI tabbed shell matching current tabs | Lets the app “look native” quickly[^12] |
| 3 | AppKit bridges for folder picker, stay-on-top, beep, review | Covers the Mac-only edges[^3][^18] |
| 4 | StoreKit 2 entitlement layer + restore flow | Required for paid feature continuity[^4][^9][^30] |
| 5 | Upgrade testing from the shipping Xamarin build | Proves the migration story[^5][^17] |

## Risks and hard truths

| Risk | Reality | Mitigation |
|---|---|---|
| Older macOS support | Current app claims macOS 10.10, but pure SwiftUI `App` needs macOS 11 and StoreKit 2 needs macOS 12 | Raise min OS for the Swift rewrite, or keep/freeze the legacy binary for older users[^5][^9][^10] |
| Bookmark migration | Saved folder access can fail if the bookmark is stale or signing identity changes | Keep same app identity; fall back to reprompting for folder selection[^3][^8][^28] |
| Purchase regression | Changing product IDs or app identity breaks continuity | Keep the exact existing product IDs and app listing[^4][^7] |
| Output-format drift | .NET `TimeSpan` formatting is easy to accidentally change in Swift | Create parity tests from the current implementation[^13][^26] |
| Behavior drift on close/minimize | Current app quits when the last window closes | Preserve that behavior unless you intentionally add a menubar mode[^1] |

## Bottom-line recommendation

**Yes — convert it directly to a native SwiftUI macOS app written in Swift.** Do it as a **compatibility-preserving rewrite** shipped as an **update to the existing Mac App Store app**, not as a new app. Keep the existing bundle ID, URL scheme, default folder model, `UserDefaults` keys, bookmark key, and StoreKit product IDs. That will preserve the user’s upgrade path, purchases, and automation ecosystem.[^4][^5][^7][^17]

The only meaningful thing you cannot preserve exactly is the **ancient macOS support floor**. If “100% backwards compatibility” includes users still on macOS 10.10-10.14, a pure SwiftUI rewrite cannot satisfy that requirement. If “100% backwards compatibility” means users’ **settings, purchases, and workflows survive the upgrade**, then the plan above is the right one.[^5][^9][^10][^11]

## Confidence Assessment

**High confidence** on the current feature inventory, compatibility surface, and migration-critical behaviors because those findings come directly from the checked-in Mac, UI, Shared, and StreamDeck projects.[^1][^2][^3][^4][^12][^14][^17]

**High confidence** on the StoreKit 2 and macOS background-execution guidance because those findings are backed by current Apple documentation.[^7][^9][^11][^30][^31]

**Medium confidence** on the exact persistence mechanics of `Plugin.Settings` on macOS because the repo shows its usage but not the plugin’s internal source. The safe assumption is still to preserve the same `UserDefaults` keys and verify on a real upgrade from the shipping app before release.[^17][^27]

## Footnotes

[^1]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Mac/AppDelegate.cs:54-143`
[^2]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/ViewModel/TimerViewModel.cs:391-549,817-838`
[^3]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Mac/Services/PlatformHelpers.cs:18-40,52-68,86-183,191-280`
[^4]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/ViewModel/ProViewModel.cs:14-31,83-95,125-167,225-370,451-499`
[^5]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Mac/Info.plist:5-12,31-47`
[^6]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Mac/Entitlements.plist:5-10`
[^7]: Apple App Store Connect, “Configure In-App Purchase settings,” which states that a single in-app purchase can be shared across multiple platform versions of the same app: `https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/overview-for-configuring-in-app-purchases/`
[^8]: Apple Foundation docs, `NSURL.bookmarkData(options:includingResourceValuesForKeys:relativeTo:)`, security-scoped bookmark guidance: `https://docs.developer.apple.com/tutorials/data/documentation/foundation/nsurl/bookmarkdata(options:includingresourcevaluesforkeys:relativeto:).md`
[^9]: Apple StoreKit docs, `Transaction.currentEntitlements` availability `macOS: 12.0.0 -`: `https://docs.developer.apple.com/tutorials/data/documentation/storekit/transaction/currententitlements.md`
[^10]: Apple SwiftUI docs, `App` protocol availability `macOS: 11.0.0 -`: `https://docs.developer.apple.com/tutorials/data/documentation/swiftui/app.md`
[^11]: Apple Xcode docs, “Configuring background execution modes,” note: “The Background Modes capability isn’t available for macOS apps.” `https://docs.developer.apple.com/tutorials/data/documentation/xcode/configuring-background-execution-modes.md`
[^12]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/MainPage.xaml:2-16`
[^13]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabDownPage.xaml:21-205`
[^14]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/Helpers/Utils.cs:6-122`
[^15]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/ViewModel/TimerViewModel.cs:35-38`
[^16]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabTimePage.xaml:15-103`
[^17]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/Helpers/Settings.cs:14-132,133-279`
[^18]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/ViewModel/AboutViewModel.cs:39-49`
[^19]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabDownPage.xaml.cs:16-49`; `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabUpPage.xaml.cs:17-48`; `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabTimePage.xaml.cs:17-34`
[^20]: `/Users/jamesmontemagno/Projects/MyStreamTimer/README.md:13-50,56-67`
[^21]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.StreamDeck/Actions/CountdownAction.cs:9-23`
[^22]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.StreamDeck/Actions/TimeAction.cs:9-25`
[^23]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.StreamDeck/Actions/TopOfTheHourAction.cs:9-24`
[^24]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/TabAboutPage.xaml.cs:18-90`
[^25]: Apple SwiftUI docs, `.onOpenURL(perform:)`: `https://docs.developer.apple.com/tutorials/data/documentation/swiftui/view/onopenurl(perform:).md`
[^26]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.Shared/ViewModel/TimerViewModel.cs:415-549,579-786`
[^27]: Apple Foundation docs, `UserDefaults`: `https://docs.developer.apple.com/tutorials/data/documentation/foundation/userdefaults.md`
[^28]: Apple App Sandbox / security-scoped bookmark guidance as summarized in Apple documentation and archived App Sandbox guidance linked from the bookmark APIs: `https://developer.apple.com/documentation/security/security-scoped_bookmarks`
[^29]: `/Users/jamesmontemagno/Projects/MyStreamTimer/MyStreamTimer.UI/MainPage.xaml.cs:89-186`
[^30]: Apple StoreKit docs, `AppStore.sync()` and `Transaction.updates`: `https://docs.developer.apple.com/tutorials/data/documentation/storekit/appstore/sync().md` and `https://docs.developer.apple.com/tutorials/data/documentation/storekit/transaction/updates.md`
[^31]: Apple Foundation docs, `ProcessInfo.beginActivity(options:reason:)`: `https://docs.developer.apple.com/tutorials/data/documentation/foundation/processinfo/beginactivity(options:reason:).md`
[^32]: Apple SwiftUI docs, `MenuBarExtra` availability `macOS: 13.0.0 -`: `https://docs.developer.apple.com/tutorials/data/documentation/swiftui/menubarextra.md`
[^33]: Microsoft docs, “Upgrade from Xamarin to .NET,” including the note that Xamarin support ended on May 1, 2024: `https://learn.microsoft.com/dotnet/maui/migration/?view=net-maui-10.0`
