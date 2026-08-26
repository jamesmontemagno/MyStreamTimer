# WinUI 3.0 — what's left for *you* to ship

Everything below needs a human: admin rights, Partner Center, or repo settings. The code, tests, CI and packaging
recipes are done on branch `winui-rewrite` (draft PR [#82](https://github.com/jamesmontemagno/MyStreamTimer/pull/82)).
Work top‑to‑bottom; each section says what to do, where, and how to verify.

Key facts you'll need repeatedly:

| Item | Value |
|---|---|
| Store app | **My Stream Timer**, Store ID **9N5NXX3WK7K7** |
| Package identity | `23875RefractoredLLC.MyStreamTimer`; **Store** Publisher `CN=995C4AD9-3B22-4B61-B30F-3EAB23CDAEAE` → PFN `23875RefractoredLLC.MyStreamTimer_jcspp7mzn01xr` (`Package.Store.appxmanifest`, `-p:MyStreamTimerStoreBuild=true`). Sideload/CI builds use `Package.appxmanifest` with `CN=Refractored LLC` (different PFN `…_xv92rx4s8jzv8`, **not** an in‑place upgrade of the Store app) |
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
2. Build + package the Release x64 app with the **Store identity** (the PFN only matches the live app when the Store
   flavour is built; a cert whose subject equals the Store publisher GUID is needed to sideload it):
   ```powershell
   dotnet build src\MyStreamTimer.WinUI\MyStreamTimer.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:MyStreamTimerStoreBuild=true -p:SelfContained=false -p:WindowsAppSDKSelfContained=false
   winapp cert generate --manifest src\MyStreamTimer.WinUI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\appxmanifest.xml --output artifacts\refractored-dev.pfx --if-exists skip
   winapp package src\MyStreamTimer.WinUI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64 --cert artifacts\refractored-dev.pfx --output artifacts\MyStreamTimer_3.0.0.0_x64.msix
   ```
   (Delete an older `artifacts\refractored-dev.pfx` first if it was generated for `CN=Refractored LLC`.)
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

Lets `windows-store-publish.yml` submit the `.msixupload` to Partner Center automatically (same flow as
[tiny-clips](https://github.com/jamesmontemagno/tiny-clips/blob/main/.github/workflows/windows-store-publish.yml)).

1. Partner Center → **Account settings → User management → Microsoft Entra applications → Add Microsoft Entra
   application** (create a new one or pick an existing app registration). Role: **Manager** (needs submission rights).
   Registering the app only in the Entra portal is **not** enough — it must be associated here.
2. Note: **Tenant ID**, **Client ID**, and create a **Client secret** (copy it once). **Seller ID** is the numeric id on
   *Account settings → Account details* (not a GUID).
3. Confirm the Store identity: *Product management → Product identity* must show Package/Identity/Name
   `23875RefractoredLLC.MyStreamTimer`, Publisher `CN=995C4AD9-3B22-4B61-B30F-3EAB23CDAEAE` and PFN
   `23875RefractoredLLC.MyStreamTimer_jcspp7mzn01xr` — that is what `src\MyStreamTimer.WinUI\Package.Store.appxmanifest`
   declares (the PFN hash is derived from the GUID publisher, not from `CN=Refractored LLC`).

---

## 4. GitHub repository secrets & variables (≈10 min)

Create an **Environment** named `microsoft-store` (`Settings → Environments → New environment`) and add yourself as a
*required reviewer* so every Store submission waits for a manual approval. Put the secrets **on that environment**,
and the product id as a **repository variable**.

| Type | Name | Value |
|---|---|---|
| Environment secret | `PARTNER_CENTER_TENANT_ID` | Entra tenant ID (from §3) |
| Environment secret | `PARTNER_CENTER_SELLER_ID` | Partner Center seller ID (numeric) |
| Environment secret | `PARTNER_CENTER_CLIENT_ID` | Entra app (client) ID |
| Environment secret | `PARTNER_CENTER_CLIENT_SECRET` | the client secret |
| **Repository variable** | `MICROSOFT_STORE_PRODUCT_ID` | `9N5NXX3WK7K7` |

Verify before tagging: *Actions → Windows Store Publish → Run workflow* with **diagnose_only = true**. The
`diagnose-partner-center` action requests an Entra token and calls the submission API, and tells you exactly which of
the four values is wrong (expired secret, wrong tenant, app not associated with Partner Center, …).

Nothing else is required: `ci.yml` runs on pushes/PRs with no secrets. Store submissions are signed by Microsoft, so no
signing certificate is involved.

---

## 5. Ship (≈ an afternoon incl. Store review)

1. Review/merge PR #82 into `main` (CI must be green — it is).
2. Dry run without publishing: *Actions → Windows Store Publish → Run workflow* with `tag = v3.0.0-windows`,
   `publish = false`. Download the `windows-store-package` artifact and sanity‑check `MyStreamTimer-3.0.0.0.msixupload`
   (optionally upload it by hand in Partner Center as a **Package Flight** to test the real Store signing/upgrade path on
   a second machine that has the Store 2.6.2 — then walk the checklist from §1 step 4).
3. Tag and push (Windows tags carry a `-windows` suffix; Mac uses `-mac`):
   ```powershell
   git checkout main; git pull
   .\scripts\create-release-tags.ps1 v3.0.0 -Windows -Push
   ```
   `windows-store-publish.yml` will: stamp `Package.Store.appxmanifest` from the tag (`v3.0.0-windows` → `3.0.0.0`,
   `v3.0.0.1-windows` → `3.0.0.1`) → build the framework‑dependent x64 + ARM64 bundle
   (`-p:MyStreamTimerStoreBuild=true … UapAppxPackageBuildMode=StoreUpload`) → upload the `.msixupload` artifact → wait
   for the `microsoft-store` environment approval → diagnose credentials → `msstore publish <file> --appId 9N5NXX3WK7K7
   --uploadTimeout 900`. Approve the gate in the Actions run; the submission then appears in Partner Center.
4. In Partner Center finish the submission (release notes: README "What's new in 3.0" + "ARM32 no longer supported,
   requires Windows 10 1809+"), and submit for certification.
5. Sandbox the purchases once live/in flight: Pro page shows prices for Lifetime / Monthly / 6 Months, **Subscribe** a
   subscription, relaunch → "Pro subscription active until …"; **Restore purchases** on a clean profile.
6. After it's live: watch Partner Center **Health** (crashes) and **Reviews** for two weeks; hotfix = bump version,
   `.\scripts\create-release-tags.ps1 v3.0.1 -Windows -Push`.

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
| Store upload locally | `dotnet build src\MyStreamTimer.WinUI -c Release -p:Platform=x64 -p:MyStreamTimerStoreBuild=true -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir=artifacts\store\ -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|ARM64" -p:UapAppxPackageBuildMode=StoreUpload -p:AppxPackageSigningEnabled=false -p:AppxSymbolPackageEnabled=false` → `artifacts\store\*.msixupload` (same command as the workflow) |
| Dump a legacy `settings.dat` | `dotnet run --project eng\SettingsDump -- <settings.dat>` |
| Regenerate icons from the Mac asset | `dotnet run --project eng\IconGen -- <glyph.png> src\MyStreamTimer.WinUI\Assets Art` |
