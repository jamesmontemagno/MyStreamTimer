# WinUI 3.0 — what's left for *you* to ship

Everything below needs a human: admin rights, Partner Center, or repo settings. The code, tests, CI and packaging
recipes are done on branch `winui-rewrite` (draft PR [#82](https://github.com/jamesmontemagno/MyStreamTimer/pull/82)).
Work top‑to‑bottom; each section says what to do, where, and how to verify.

Key facts you'll need repeatedly:

| Item | Value |
|---|---|
| Store app | **My Stream Timer**, Store ID **9N5NXX3WK7K7** |
| Package identity | `23875RefractoredLLC.MyStreamTimer`, Publisher `CN=Refractored LLC`, PFN `23875RefractoredLLC.MyStreamTimer_jcspp7mzn01xr` |
| Version | `3.0.0.0` (must be > the live `2.6.2.0`) |
| Protocol | `mystreamtimer://` (Debug builds use `mystreamtimer-dev://` and a `*.Dev` identity) |
| Add‑on IDs | sold: `mstgold` (Lifetime), `mstsub` (1 month), `mstsub6months` (6 months) · legacy (still honoured, not sold): `mstbronze`, `mstsilver` |
| Min OS | Windows 10 1809 (17763); architectures x64 + ARM64 (ARM32 dropped) |

---

## 1. Local upgrade test (≈30 min, needs **Administrator** PowerShell)

Proves that installing 3.0 over the live Store build keeps every setting, the output folder, Pro flags and the
protocol handler.

1. Make sure the Store build is installed (`winget install 9N5NXX3WK7K7 --source msstore`), launch it once, and set a
   few non‑default values (any timer: minutes, output text, file name, auto‑start). Start/stop a timer so the `*.txt`
   files exist.
2. Build + package the Release x64 app with the **Store identity** (already done → `artifacts\`; regenerate if you
   changed code):
   ```powershell
   dotnet build src\MyStreamTimer.WinUI\MyStreamTimer.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:UseDevIdentity=false
   winapp package src\MyStreamTimer.WinUI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64 --cert artifacts\refractored-dev.pfx --output artifacts\MyStreamTimer_3.0.0.0_x64.msix
   ```
3. **Admin** PowerShell:
   ```powershell
   .\winui-migration\Run-UpgradeTest.ps1          # trusts the dev cert, runs WACK (artifacts\wack.xml), installs 3.0 over 2.6.2
   ```
   It throws if the Package Family Name changed (i.e. it was *not* an in‑place upgrade).
4. Launch the app and walk `winui-migration\upgrade-test-checklist.md` (settings present, same folder, OBS source still
   updates, `start mystreamtimer://countdown2/?secs=90` routes to the running instance, Pro flags honoured offline).
5. Put the machine back: `.\winui-migration\Run-UpgradeTest.ps1 -Restore` (removes the sideload, reinstalls the Store build).

✅ Done when: WACK = PASS (or only "Supported API" warnings about WinAppSDK internals), checklist all green.

---

## 2. Partner Center — products (≈20 min)

Dashboard → **My Stream Timer** (9N5NXX3WK7K7).

### 2a. Create the two subscription add‑ons
**Add‑ons → Create a new add‑on**, twice:

| Field | Monthly | 6 Months |
|---|---|---|
| Product type | **Subscription** | **Subscription** |
| Product ID | `mstsub` | `mstsub6months` |
| Subscription period | 1 month | 6 months |
| Free trial | optional (none is fine) | optional |
| Pricing | your choice (Mac uses the same IDs) | your choice |
| Visibility | Can be displayed in the parent product's Store listing | same |
| Properties → Content type | Software as a service | same |
| Store listing | Title e.g. "My Stream Timer Pro — Monthly", short description | "… — 6 Months" |

Submit both. They must be **published** before the app can list prices/purchase them (the app hides prices as "—" until
the Store returns them).

### 2b. Confirm the lifetime add‑on
`mstgold` already exists as a **Durable**; leave it. Optionally rename its listing title to "Lifetime". Leave
`mstbronze` / `mstsilver` published but you may mark them **Hidden** in the Store (`Visibility → Hidden in Store`) —
existing owners keep their licence and the app still honours them.

### 2c. Store listing refresh (while you're there)
- Screenshots: use `winui-migration\screenshots\dashboard-light.png`, `dashboard-dark.png`, `timer-running-light.png`,
  `p5-02-settings-light.png`, `gate-popout.png`, `pro-catalogue.png` (crop as needed; Store wants ≥ 1366×768 or 768×1366).
- Description: add the 3.0 bullets from `README.md` "What's new in 3.0".
- **System requirements**: Windows 10 version 1809 or later; remove ARM32 if listed.
- Age rating / privacy policy URL (`https://refractored.com/about/`) are unchanged.

---

## 3. Partner Center — API access for CI publishing (≈15 min)

Lets `release.yml` push packages to a flight automatically.

1. Partner Center → **Account settings → User management → Azure AD applications → Add Azure AD application**
   (create a new one or pick an existing app registration). Role: **Manager** (needs submission rights).
2. Note: **Tenant ID**, **Client ID**, and create a **Client secret** (copy it once). **Seller ID** is on
   *Account settings → Legal info* (or the URL of Partner Center).
3. (Optional but recommended) Create a **Package Flight**: *App → Package flights → New flight*, name
   `3.0 Insiders`, add yourself/testers, note the **Flight ID** from the URL.

---

## 4. GitHub repository secrets & variables (≈10 min)

`Settings → Secrets and variables → Actions`. Also create an **Environment** named `release`
(`Settings → Environments → New environment`) — add yourself as a *required reviewer* so production publishes wait for
a manual approval.

| Type | Name | Value |
|---|---|---|
| Secret | `STORE_TENANT_ID` | Azure AD tenant ID (from §3) |
| Secret | `STORE_SELLER_ID` | Partner Center seller ID |
| Secret | `STORE_CLIENT_ID` | Azure AD app (client) ID |
| Secret | `STORE_CLIENT_SECRET` | the client secret |
| Secret | `SIGNING_PFX_BASE64` | *(optional — only for side‑loadable GitHub Release assets)* base64 of a code‑signing PFX whose subject is **`CN=Refractored LLC`**. Store submissions are re‑signed by Microsoft, so this is **not** needed for the Store path. Create with `[Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx')) | Set-Clipboard` |
| Secret | `SIGNING_PFX_PASSWORD` | its password |
| **Variable** | `STORE_FLIGHT_ID` | Flight ID from §3 (leave empty/unset to publish straight to production) |

Nothing else is required: `ci.yml` already runs on pushes/PRs with no secrets.

---

## 5. Ship (≈ an afternoon incl. Store review)

1. Review/merge PR #82 into `main` (CI must be green — it is).
2. Tag and push:
   ```powershell
   git checkout main; git pull
   git tag v3.0.0-rc.1; git push origin v3.0.0-rc.1
   ```
   `release.yml` will: test → build x64 + ARM64 (Store identity, version stamped from the tag) → bundle →
   (sign if PFX secret present) → **publish to the flight** (if `STORE_FLIGHT_ID` set) → create a GitHub Release with the
   `.msixbundle`. The `release` environment gate pauses before publishing; approve it in the Actions run.
3. Install from the flight on a second machine that has the Store 2.6.2 → repeat the checklist from §1 step 4 (this
   is the only end‑to‑end test of the real Store signing/upgrade path).
4. Sandbox the purchases on that machine: Pro page shows prices for Lifetime / Monthly / 6 Months, **Subscribe** a
   subscription, relaunch → "Pro subscription active until …"; **Restore purchases** on a clean profile.
5. Production: either set `STORE_FLIGHT_ID` empty and tag `v3.0.0`, or in Partner Center *Promote* the flight
   submission. Add release notes (use the README "What's new in 3.0" + "ARM32 no longer supported, requires Windows 10
   1809+").
6. After it's live: watch Partner Center **Health** (crashes) and **Reviews** for two weeks; hotfix = bump version,
   tag `v3.0.1`.

---

## 6. Nice‑to‑have / not blocking

- Delete the `legacy/` Xamarin/UWP/Mac projects once 3.0 has been live for a cycle (`git rm -r legacy`).
- macOS parity: rename timers + icons ([#81](https://github.com/jamesmontemagno/MyStreamTimer/issues/81)); consider a
  Home dashboard on Mac too.
- Trimming (`PublishTrimmed`) was evaluated and left **off** for 3.0 (see plan P8‑5); revisit for 3.1 with the upgrade
  rig from §1.
- Dependabot is not yet enabled for NuGet/Actions (`Settings → Code security → Dependabot`).
- If you ever rotate the publisher certificate, nothing in the repo needs to change — identity lives in the manifest,
  signing happens in the Store/CI.

---

### Quick commands reference
| Need | Command |
|---|---|
| Run Debug (Dev identity) | `cd src\MyStreamTimer.WinUI; dotnet run` or `& "$env:USERPROFILE\.copilot\installed-plugins\win-dev-skills\winui\skills\winui-dev-workflow\BuildAndRun.ps1"` |
| Core tests | `dotnet test tests\MyStreamTimer.Core.Tests -c Release` |
| UI smoke + a11y audit | `.\winui-migration\ui-smoke.ps1` (app running) |
| Store bundle locally | build x64 + ARM64 Release (`-p:UseDevIdentity=false`), then `makeappx pack` each and `makeappx bundle` (recipe in plan P7‑4; last output: `artifacts\MyStreamTimer_3.0.0.0.msixbundle`) |
| Dump a legacy `settings.dat` | `dotnet run --project eng\SettingsDump -- <settings.dat>` |
| Regenerate icons from the Mac asset | `dotnet run --project eng\IconGen -- <glyph.png> src\MyStreamTimer.WinUI\Assets Art` |
