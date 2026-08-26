# Upgrade test checklist — legacy (Xamarin.Forms/UWP 2.6.2) → WinUI 3.0

Run on **two machines** (Windows 10 1809+ and Windows 11). Record results in the PR.

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
