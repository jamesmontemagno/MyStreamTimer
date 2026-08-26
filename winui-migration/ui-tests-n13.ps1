param([Parameter(Mandatory)][int]$AppPid)
$ErrorActionPreference = 'Continue'
$pass = 0; $fail = 0; $results = @()
$shots = 'G:\mystreamtimer\winui-migration\screenshots'

function Test-UI {
    param([string]$Name, [scriptblock]$Script)
    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) { $script:pass++; $script:results += @{ name = $Name; status = 'PASS' } }
        else { $script:fail++; $script:results += @{ name = $Name; status = 'FAIL'; detail = "$output" } }
    } catch { $script:fail++; $script:results += @{ name = $Name; status = 'FAIL'; detail = "$_" } }
}

Test-UI 'Nav ready' { winapp ui wait-for 'NavCountdown2' -a $AppPid -t 5000 }
Test-UI 'Countdown 2 default name' { winapp ui wait-for 'NavCountdown2' -a $AppPid --value 'Countdown 2' -t 2000 }

# Countdown 1 page: preview text visible, all 5 action buttons present
Test-UI 'Go to Countdown 1' { winapp ui invoke 'NavCountdown1' -a $AppPid }
Test-UI 'Hero preview visible' { winapp ui wait-for 'HeroPreviewText' -a $AppPid --value 'Starting in' --contains -t 3000 }
Test-UI 'Reset button visible' { winapp ui wait-for 'ResetButton' -a $AppPid -t 2000 }
winapp ui screenshot -a $AppPid -o "$shots\timer-idle-preview.png" 2>$null | Out-Null

# Settings
Test-UI 'Open Settings (Ctrl+,)' { winapp ui send-keys 'ctrl+oem_comma' -a $AppPid --via send-input }
Test-UI 'Timers expander present' { winapp ui wait-for 'TimersExpander' -a $AppPid -t 4000 }
Test-UI 'Countdown 2 name box present' { winapp ui wait-for 'TimerName_countdown2' -a $AppPid -t 3000 }
Test-UI 'Rename to Giveaway' { winapp ui set-value 'TimerName_countdown2' 'Giveaway' -a $AppPid }
Test-UI 'Sidebar shows Giveaway' { winapp ui wait-for 'NavCountdown2' -a $AppPid --value 'Giveaway' -t 3000 }
Test-UI 'Open icon picker' { winapp ui invoke 'TimerIcon_countdown2' -a $AppPid }
Start-Sleep -Milliseconds 700
Test-UI 'Pick Gift icon' { winapp ui click 'Gift' -a $AppPid }
Start-Sleep -Milliseconds 500
winapp ui screenshot -a $AppPid -o "$shots\settings-timers.png" 2>$null | Out-Null

# Navigate to renamed timer
Test-UI 'Go to Giveaway timer' { winapp ui invoke 'NavCountdown2' -a $AppPid }
Test-UI 'Page header says Giveaway' { winapp ui wait-for 'TimerTitle' -a $AppPid --value 'Giveaway' -t 3000 }
Test-UI 'Header icon present' { winapp ui wait-for 'TimerTitleIcon' -a $AppPid -t 2000 }
winapp ui screenshot -a $AppPid -o "$shots\timer-renamed.png" 2>$null | Out-Null

# Reset back
Test-UI 'Back to Settings' { winapp ui send-keys 'ctrl+oem_comma' -a $AppPid --via send-input }
Test-UI 'Reset button present' { winapp ui wait-for 'TimerAppearanceReset_countdown2' -a $AppPid -t 4000 }
Test-UI 'Reset Countdown 2' { winapp ui invoke 'TimerAppearanceReset_countdown2' -a $AppPid }
Test-UI 'Sidebar back to Countdown 2' { winapp ui wait-for 'NavCountdown2' -a $AppPid --value 'Countdown 2' -t 3000 }
Test-UI 'Name box empty' { winapp ui wait-for 'TimerName_countdown2' -a $AppPid --value '' -t 2000 }

Write-Host "`nPassed: $pass | Failed: $fail"
$results | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object { Write-Host "  FAIL: $($_.name) — $($_.detail)" -ForegroundColor Red }
