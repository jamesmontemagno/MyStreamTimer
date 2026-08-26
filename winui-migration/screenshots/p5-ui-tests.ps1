param([Parameter(Mandatory)][int]$AppPid)
$ErrorActionPreference = 'Continue'
$pass = 0; $fail = 0; $results = @()
$shots = "G:\mystreamtimer\winui-migration\screenshots"
function Test-UI {
    param([string]$Name, [scriptblock]$Script)
    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) { $script:pass++; $script:results += @{ name = $Name; status = "PASS" } }
        else { $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$output" } }
    } catch { $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$_" } }
}
function Shot($name) { winapp ui screenshot -a $AppPid -o "$shots\$name.png" 2>$null | Out-Null }

# ---- Settings ----
Test-UI "Nav to Settings" { winapp ui invoke "SettingsItem" -a $AppPid }
Test-UI "Settings: OutputFolderExpander" { winapp ui wait-for "OutputFolderExpander" -a $AppPid -t 5000 }
Test-UI "Settings: ThemeRadioButtons" { winapp ui wait-for "ThemeRadioButtons" -a $AppPid -t 3000 }
Test-UI "Settings: StayOnTopToggle" { winapp ui wait-for "StayOnTopToggle" -a $AppPid -t 3000 }
Test-UI "Settings: PopOutFontSizeSlider (Pro)" { winapp ui wait-for "PopOutFontSizeSlider" -a $AppPid -t 3000 }
Test-UI "Settings: CopyFolderPathButton" { winapp ui invoke "CopyFolderPathButton" -a $AppPid }
Start-Sleep -Milliseconds 600
Test-UI "Settings: clipboard has folder path" { if ((Get-Clipboard) -match 'MyStreamTimer|Documents|\\') { $global:LASTEXITCODE = 0 } else { throw "clipboard: $(Get-Clipboard)" } }
Shot "p5-02-settings-light"
Test-UI "Settings: Test access" { winapp ui invoke "TestAccessButton" -a $AppPid }
Test-UI "Settings: InfoBar shown" { winapp ui wait-for "FolderStatusInfoBar" -a $AppPid -t 3000 }
Test-UI "Settings: Theme -> Dark" { winapp ui invoke "ThemeDark" -a $AppPid }
Start-Sleep 1
Shot "p5-03-settings-dark"
Test-UI "Settings: Theme -> System" { winapp ui invoke "ThemeSystem" -a $AppPid }
Start-Sleep 1
Test-UI "Settings: open text colour picker" { winapp ui invoke "PopOutTextColorButton" -a $AppPid }
Start-Sleep 1
Test-UI "Settings: ColorPicker visible" { winapp ui wait-for "PopOutTextColorPicker" -a $AppPid -t 3000 }
Shot "p5-04-settings-colorpicker"
winapp ui send-keys "escape" -a $AppPid --via send-input 2>$null | Out-Null
Start-Sleep -Milliseconds 500

# ---- Automation ----
Test-UI "Nav to Automation" { winapp ui invoke "NavAutomation" -a $AppPid }
Test-UI "Automation: GeneratedUrlTextBox" { winapp ui wait-for "GeneratedUrlTextBox" -a $AppPid -t 5000 }
Test-UI "Automation: default URL" { winapp ui wait-for "GeneratedUrlTextBox" -a $AppPid --value "mystreamtimer://countdown/?mins=5" -t 3000 }
Test-UI "Automation: Copy generated" { winapp ui invoke "CopyGeneratedUrlButton" -a $AppPid }
Start-Sleep -Milliseconds 600
Test-UI "Automation: clipboard has URL" { if ((Get-Clipboard) -eq 'mystreamtimer://countdown/?mins=5') { $global:LASTEXITCODE = 0 } else { throw "clipboard: $(Get-Clipboard)" } }
winapp ui send-keys "escape" -a $AppPid --via send-input 2>$null | Out-Null
Shot "p5-05-automation"
Test-UI "Automation: Run" { winapp ui invoke "RunGeneratedUrlButton" -a $AppPid }
Test-UI "Automation: Run status shown" { winapp ui wait-for "RunStatusInfoBar" -a $AppPid -t 3000 }
Start-Sleep -Milliseconds 500
Shot "p5-06-automation-ran"

# ---- Pro ----
Test-UI "Nav to Pro" { winapp ui invoke "NavPro" -a $AppPid }
Test-UI "Pro: status banner" { winapp ui wait-for "ProStatusBanner" -a $AppPid -t 5000 }
Test-UI "Pro: RestorePurchasesButton" { winapp ui wait-for "RestorePurchasesButton" -a $AppPid -t 3000 }
Start-Sleep 1
Shot "p5-07-pro"

# ---- About ----
Test-UI "Nav to About" { winapp ui invoke "NavAbout" -a $AppPid }
Test-UI "About: version" { winapp ui wait-for "AboutVersion" -a $AppPid --value "Version" --contains -t 5000 }
Test-UI "About: OpenCrashLogsButton" { winapp ui wait-for "OpenCrashLogsButton" -a $AppPid -t 3000 }
Shot "p5-08-about"

# ---- Pop-out ----
Test-UI "Nav to Countdown 1" { winapp ui invoke "NavCountdown1" -a $AppPid }
Test-UI "Timer: StartStopButton" { winapp ui wait-for "StartStopButton" -a $AppPid -t 5000 }
Test-UI "Timer: Start" { winapp ui invoke "StartStopButton" -a $AppPid }
Start-Sleep 1
Test-UI "Timer: Pop-out" { winapp ui invoke "PopOutButton" -a $AppPid }
Start-Sleep 2
$wins = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
$pop = $wins | Where-Object { $_.title -match 'Countdown 1' } | Select-Object -First 1
if ($pop) { $pass++; $results += @{ name = "Pop-out window exists"; status = "PASS" } } else { $fail++; $results += @{ name = "Pop-out window exists"; status = "FAIL"; detail = ($wins | ConvertTo-Json -Compress) } }
if ($pop) {
    Test-UI "Pop-out: text element" { winapp ui wait-for "PopOutText" -w $pop.hwnd -t 3000 }
    winapp ui screenshot -w $pop.hwnd -o "$shots\p5-09-popout.png" --capture-screen 2>$null | Out-Null
    winapp ui screenshot -a $AppPid -o "$shots\p5-10-main-with-popout.png" --capture-screen 2>$null | Out-Null
    Test-UI "Pop-out: focus root" { winapp ui focus "PopOutRoot" -w $pop.hwnd }
    Test-UI "Pop-out: ESC closes" { winapp ui send-keys "escape" -w $pop.hwnd --via send-input }
    Start-Sleep 1
    $wins2 = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
    if (-not ($wins2 | Where-Object { $_.title -match 'Countdown 1' })) { $pass++; $results += @{ name = "Pop-out closed by ESC"; status = "PASS" } } else { $fail++; $results += @{ name = "Pop-out closed by ESC"; status = "FAIL"; detail = ($wins2 | ConvertTo-Json -Compress) } }
}
Test-UI "Timer: Stop" { winapp ui invoke "StartStopButton" -a $AppPid }

Write-Host "`nPassed: $pass | Failed: $fail"
$results | Where-Object { $_.status -eq "FAIL" } | ForEach-Object { Write-Host "  FAIL: $($_.name) - $($_.detail)" -ForegroundColor Red }
