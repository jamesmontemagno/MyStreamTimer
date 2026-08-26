# UI smoke + accessibility-name audit for My Stream Timer (WinUI 3) using the `winapp ui` UIA harness.
# Usage: .\ui-smoke.ps1 [-AppPid <pid>]  (app must be running; Debug build uses the Dev identity)
param([int]$AppPid)

$ErrorActionPreference = 'Stop'
if (-not $AppPid) { $AppPid = (Get-Process MyStreamTimer | Select-Object -First 1).Id }
# Target the main window explicitly so transient popups (tooltips/flyouts) never hijack the session.
$hwnd = (winapp ui list-windows -a $AppPid 2>$null | Select-String -Pattern 'HWND (\d+): "My Stream Timer"').Matches[0].Groups[1].Value
if (-not $hwnd) { throw "Main window not found for PID $AppPid" }
$T = @('-a', $AppPid, '-w', $hwnd)
$shots = Join-Path $PSScriptRoot 'screenshots'
New-Item -ItemType Directory -Force $shots | Out-Null
$fail = 0
function Step($name, [scriptblock]$body) {
    try { & $body; Write-Host "PASS  $name" -ForegroundColor Green }
    catch { $script:fail++; Write-Host "FAIL  $name -> $($_.Exception.Message)" -ForegroundColor Red }
}
function Nav($id) { winapp ui invoke $id @T | Out-Null; Start-Sleep -Milliseconds 700 }
function Shot($file) { winapp ui screenshot @T -o (Join-Path $shots $file) | Out-Null }
function Audit($page) {
    # every interactive element must expose a non-empty accessible Name
    $lines = winapp ui inspect @T -i 2>$null
    $unnamed = $lines | Where-Object { $_ -match '^\s+\S+ (Button|Edit|ComboBox|CheckBox|ListItem|Slider|Spinner|RadioButton|Hyperlink|TabItem|MenuItem)\b' -and $_ -notmatch '"' -and $_ -notmatch '^\s+(Minimize|Maximize|Close) Button' }
    if ($unnamed) { throw "Unnamed interactive elements on $page`n$($unnamed -join "`n")" }
}

$pages = @(
    @{ Id = 'NavHome';        Name = 'home' },
    @{ Id = 'NavCountdown1';  Name = 'countdown1' },
    @{ Id = 'NavCountdown4';  Name = 'countdown4' },
    @{ Id = 'NavCountUp1';    Name = 'countup1' },
    @{ Id = 'NavCurrentTime'; Name = 'time' },
    @{ Id = 'NavAutomation';  Name = 'automation' },
    @{ Id = 'NavPro';         Name = 'pro' },
    @{ Id = 'NavAbout';       Name = 'about' },
    @{ Id = 'SettingsItem';   Name = 'settings' }
)
foreach ($p in $pages) {
    Step "navigate + a11y audit: $($p.Name)" { Nav $p.Id; Audit $p.Name; Shot "audit-$($p.Name).png" }
}

Step 'countdown 1 start/stop writes file' {
    Nav 'NavCountdown1'
    winapp ui invoke 'StartStopButton' @T | Out-Null
    Start-Sleep 2
    $file = Get-ChildItem "$env:LOCALAPPDATA\Packages\RefractoredLLC.MyStreamTimer.Dev_*\LocalState\ProgramData\MyStreamTimer\countdown.txt" | Select-Object -First 1
    $txt = Get-Content $file.FullName -Raw
    if ($txt -notmatch 'Starting in \d\d:\d\d:\d\d') { throw "unexpected file text '$txt'" }
    Shot 'smoke-running.png'
    winapp ui invoke 'StartStopButton' @T | Out-Null
    winapp ui wait-for 'StartStopButton' --property Name --value 'Start' --timeout-ms 5000 @T | Out-Null
    Start-Sleep 1
    $after = Get-Content $file.FullName -Raw
    if ("$after" -ne '') { throw "file not cleared after stop: '$after'" }
}

Step 'automation builder generates URL' {
    Nav 'NavAutomation'
    $v = winapp ui get-value 'GeneratedUrlTextBox' @T
    if ($v -notmatch '^mystreamtimer://') { throw "bad url '$v'" }
}

Step 'home dashboard shows 7 timer cards' {
    Nav 'NavHome'
    Audit 'home'
    $cards = winapp ui search 'Card' @T 2>$null | Where-Object { $_ -match '^\s*Dash\w+Card Group' }
    if ($cards.Count -ne 7) { throw "expected 7 dashboard cards, found $($cards.Count)" }
    if (-not (winapp ui search 'DashCountdown' @T 2>$null | Where-Object { $_ -match 'DashCountdownStartStop' })) { throw 'DashCountdownStartStop missing' }
    winapp ui invoke 'DashCountdownStartStop' @T | Out-Null
    winapp ui wait-for 'DashCountdownCard' --value 'Countdown 1, Running' --timeout-ms 5000 @T | Out-Null
    Shot 'dashboard-running.png'
    winapp ui invoke 'DashStopAll' @T | Out-Null
    winapp ui wait-for 'DashCountdownCard' --value 'Countdown 1, Idle' --timeout-ms 5000 @T | Out-Null
}

Write-Host "`nDone. Failures: $fail"
exit $fail




