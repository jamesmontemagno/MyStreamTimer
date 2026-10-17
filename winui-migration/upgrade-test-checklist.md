# Upgrade test checklist — legacy (Xamarin.Forms/UWP 2.6.2) → WinUI 3.0

Run on **two machines** (Windows 10 1809+ and Windows 11). Record results in the PR.

## Store stability and diagnostics gate

Use a disposable Windows 11 25H2 **x64 build 26200** VM and record its full OS revision. Do not remove runtimes or alter the Store app on the development host. Provision the VM and a licensed OS image before claiming target-OS validation.

- [ ] Snapshot the VM without .NET 10 installed. Record installed Windows App Runtime package versions.
- [ ] On a separate snapshot, compare a retained 3.0/3.1 package before/after installing its required architecture-specific .NET runtime. Record startup errors separately from crashes.
- [ ] Install the new package through a Store flight or properly signed MSIX. Launch without separately installing .NET; verify the app and actual loaded runtimes, not just a successful build.
- [ ] Run `scripts\Test-StorePackage.ps1 -PackagePath <upload.msixupload>` against the exact upload. Verify x64 and ARM64 bundled runtime payloads and binary/PDB identity.
- [ ] Confirm publication revalidates the downloaded upload, logs its SHA-256, and submits the complete `.msixupload` containing `.appxsym` archives, not just the app bundle or a GitHub-only symbols artifact.
- [ ] Before starting any countdown, send `?subtractmins=1` and `?subtractsecs=30` to each countdown. No crash, timer start, output mutation, or false success message. Automation shows an ignored/warning result.
- [ ] Start a countdown, then add/subtract, pause/resume, reset and stop. Confirm existing paused/stopped command behavior is preserved.
- [ ] Reject nonfinite and date-overflowing numeric commands without stopping/replacing an already running timer or acquiring a new power request.
- [ ] Exercise cold/warm protocol activation, repeated launches, and a primary process closing normally during redirection. Record hangs separately. Confirm no duplicate app window. Cover redirection failures with isolated test doubles rather than deliberately faulting a running app.
- [ ] Check timer completion, sleep/resume, pop-out close, display/DPI changes, and offline Pro entitlement.
- [ ] Use isolated unit-test fakes for recoverable startup, command, and diagnostics failures. Confirm useful error outcomes and no automatic upload. Do not inject crashes into a running app or ship a crash-injection switch; do not blanket-handle fatal XAML errors.
- [ ] Generate enough diagnostic records to verify six-file/128-KiB-per-file retention; repeating engine faults are throttled.
- [ ] Run the four seeded `RandomizedCommandTests` (100,000 Core operations), then use `scripts\Test-WindowsRandomizedSmoke.ps1` against only a uniquely named temporary package. Retain seeds/results, distinguish disabled-action skips and activation attempts from executed actions, review screenshots, and remove only the temporary registration.
- [ ] About > Save diagnostics report: cancel, save successfully, and attempt an unwritable destination. Verify visible success/failure, excluded legacy crash logs/settings/timer content, and no source paths or exception messages in the ZIP.
- [ ] Verify the diagnostics actions at narrow widths, keyboard-only, Light/Dark, and High Contrast.
- [ ] With explicit consent, configure per-app WER capture for naturally occurring native failures. Analyze collected evidence with the release's retained binaries/symbols; remove only the test's per-app dump configuration afterward. Do not deliberately trigger a native crash.
- [ ] Retain the upload hash, commit, symbols and evidence before recommending a Store flight. Publication requires separate authorization. Do not claim a Partner Center rate reduction until comparable post-flight cohorts support it.

## Preparation
- [ ] Install the current Store build (`winget install 9N5NXX3WK7K7 --source msstore`) and launch once.
- [ ] Configure **every** timer with non-default values (see table), run Countdown 1 for a few seconds, stop.
- [ ] Note the folder shown by "Copy" (expected `%LOCALAPPDATA%\Packages\23875RefractoredLLC.MyStreamTimer_jcspp7mzn01xr\LocalState\ProgramData\MyStreamTimer`).
- [ ] If a Pro purchase is available on this account, make sure it's unlocked (or inject `IsGold=true` in `settings.dat` using a test account).
- [ ] Create an OBS text source reading `…\MyStreamTimer\countdown.txt`.

| Timer | Minutes | Seconds | Output | Finish | File name | Auto | Beep | Style | Other |
|---|---|---|---|---|---|---|---|---|---|
| Countdown 1 | 7 | 30 | `Live in {0:mm\:ss}` | `GO!` | `cd1.txt` | off | on | Custom | Clock time 15:30 chosen then back to Duration |
| Countdown 2 | 12 | 0 | default | `Done 2` | default | on | off | Auto (Pro) | |
| Countdown 3 | 1 | 5 | `{0:ss} s` | default | `three.txt` | off | off | Total seconds (Pro) | |
| Countdown 4 (Pro) | 3 | 0 | default | default | default | off | on | M:ss (Pro) | |
| Count Up 1 | — | — | `Up {0:hh\:mm\:ss}` | — | `up.txt` | on | — | Custom | |
| Count Up 2 (Pro) | — | — | default | — | default | off | — | Auto | |
| Time (Pro) | — | — | — | — | `clock.txt` | on | — | H:mm:ss | AM/PM on |

## Upgrade
- [ ] Install the 3.0 package **with the Store identity** (Package Flight, or `Add-AppxPackage` of a Release build made with `-p:MyStreamTimerStoreBuild=true` and signed with a cert whose subject is the Store publisher `CN=995C4AD9-3B22-4B61-B30F-3EAB23CDAEAE`). The install must be reported as an *update* (same PFN), not a second app.
- [ ] Launch → no crash; welcome‑back dialog shown **once**; not shown again on next launch.

## Verify
- [ ] Every value in the table is identical in the new UI (including Pro‑only styles appearing selected when Pro).
- [ ] Settings › Output folder shows the same folder as the legacy "Copy" path; the existing `*.txt` files are listed.
- [ ] Start Countdown 1 → the OBS text source (pointing at the *old* file) updates; text matches legacy format exactly.
- [ ] `start mystreamtimer://countdown2/?secs=90` from cmd: routes to the running instance (no second window), Countdown 2 selected and running.
- [ ] `?pause`, `?resume`, `?addmins=1`, `?subtractsecs=30`, `?reset`, `?stop`, `?to=HH:MM`, `?topofhour`, `countup/?mins=1`, `time/?start` behave per plan Appendix A.
- [ ] Stream Deck plugin buttons (or equivalent "Website" action) work unchanged.
- [ ] Pro: lifetime flags honoured offline (airplane mode) — Pro page shows "Unlocked"; Countdown 4 / Count Up 2 / Time usable.
- [ ] Subscriptions: purchase monthly in sandbox → status shows expiry; relaunch keeps Pro; Manage subscription link opens.
- [ ] Stay on top / theme / pop‑outs function and persist after restart; pop‑out stays above a fullscreen OBS preview when Stay on top is on.
- [ ] Machine does not sleep during a 20‑min countdown (sleep timeout 1 min); sleeps after stop.
- [ ] Beep plays 3× at zero when enabled.
- [ ] Uninstall → reinstall 3.0 → defaults restored (settings gone, expected), no crash.
