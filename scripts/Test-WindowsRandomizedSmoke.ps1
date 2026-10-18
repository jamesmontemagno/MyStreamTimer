param(
    [Parameter(Mandatory)][int] $AppPid,
    [Parameter(Mandatory)]
    [ValidatePattern('^RefractoredLLC\.MyStreamTimer\.(CrashTest|SmokeTest)[A-Za-z0-9]+$')]
    [string] $PackageName,
    [Parameter(Mandatory)][string] $Protocol,
    [Parameter(Mandatory)][string] $ResultsDirectory,
    [int] $Seed = 63801,
    [ValidateRange(1, 1000)][int] $Iterations = 120,
    [ValidateRange(30, 1800)][int] $MaximumSeconds = 600
)

$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name $PackageName
if (-not $package -or -not $package.IsDevelopmentMode) {
    throw 'Only an explicitly named, isolated development test package is allowed.'
}
$executable = Join-Path $package.InstallLocation 'MyStreamTimer.exe'
$app = Get-Process -Id $AppPid
if ($app.Path -ne $executable) { throw 'The PID does not belong to the isolated test package.' }
$manifest = [xml](Get-Content -LiteralPath (Join-Path $package.InstallLocation 'AppxManifest.xml') -Raw)
$registeredProtocols = @($manifest.Package.Applications.Application.Extensions.Extension.Protocol.Name)
if ($Protocol -notin $registeredProtocols -or $Protocol -in @('mystreamtimer', 'mystreamtimer-dev')) {
    throw 'The test protocol must belong exclusively to the isolated package.'
}

$resultsPath = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$winapp = (Get-Command winapp).Source
$random = [Random]::new($Seed)
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$steps = [System.Collections.Generic.List[object]]::new()
$uiCalls = 0
$protocolLaunches = 0
$disabledActions = 0
$faults = @{}
$mainStartTime = $app.StartTime
$logs = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState\logs"
$start = [DateTimeOffset]::UtcNow
$failure = $null
$hwnd = $null

function Invoke-Ui {
    param([string[]] $Arguments, [switch] $AllowNoMatches)
    if ($watch.Elapsed.TotalSeconds -gt $MaximumSeconds) { throw 'Smoke-test time budget exceeded.' }
    $command = [System.Diagnostics.Process]::new()
    $command.StartInfo.FileName = $winapp
    $command.StartInfo.UseShellExecute = $false
    $command.StartInfo.RedirectStandardOutput = $true
    $command.StartInfo.RedirectStandardError = $true
    $options = if ('--json' -in $Arguments) { @() } else { @('-q') }
    foreach ($argument in (@('ui') + $Arguments + $options)) {
        $command.StartInfo.ArgumentList.Add($argument)
    }
    try {
        $null = $command.Start()
        $stdout = $command.StandardOutput.ReadToEndAsync()
        $stderr = $command.StandardError.ReadToEndAsync()
        if (-not $command.WaitForExit(15000)) {
            Stop-Process -Id $command.Id
            throw "UI command timed out: $($Arguments -join ' ')"
        }
        $text = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        $script:uiCalls++
        if ($command.ExitCode -ne 0 -and -not ($AllowNoMatches -and $command.ExitCode -eq 1)) {
            throw "UI command failed ($($command.ExitCode)): $($Arguments -join ' ')`n$text`n$errors"
        }
        return $text
    } finally {
        $command.Dispose()
    }
}

function Find-Element([string] $Id) {
    $response = Invoke-Ui -Arguments @('search', $Id, '-w', "$hwnd", '--json') -AllowNoMatches | ConvertFrom-Json
    if ($response.matchCount -gt 1) { throw "Ambiguous control: $Id" }
    if ($response.matchCount -eq 1) { return $response.matches[0] }
    return $null
}

function Navigate([string] $Id) {
    Invoke-Ui -Arguments @('invoke', $Id, '-w', "$hwnd") | Out-Null
}

function Invoke-Enabled([string] $Id) {
    $element = Find-Element $Id
    if (-not $element) { throw "Expected control is missing: $Id" }
    if (-not $element.isEnabled) {
        $script:disabledActions++
        return
    }
    Invoke-Ui -Arguments @('invoke', $Id, '-w', "$hwnd") | Out-Null
}

function Check-Health {
    $current = Get-Process -Id $AppPid -ErrorAction SilentlyContinue
    if (-not $current -or $current.Path -ne $executable -or $current.StartTime -ne $mainStartTime) {
        throw 'The primary test process exited or was replaced.'
    }
    Invoke-Ui -Arguments @('wait-for', 'NavHome', '-w', "$hwnd", '-t', '3000') | Out-Null
    if (-not (Test-Path -LiteralPath $logs)) { throw 'The isolated diagnostic directory is missing.' }
    foreach ($file in Get-ChildItem -LiteralPath $logs -Filter 'diagnostics-*.jsonl') {
        foreach ($line in [System.IO.File]::ReadAllLines($file.FullName)) {
            $record = $line | ConvertFrom-Json
            if ([DateTimeOffset]$record.Timestamp -ge $start -and
                $record.Event -in @('entry-point-failure', 'managed-unhandled', 'xaml-unhandled', 'unobserved-task', 'timer-failure')) {
                $script:faults["$($record.Session):$($record.Timestamp):$($record.Event)"] = $record
            }
        }
    }
    if ($faults.Count -gt 0) { throw 'The test produced application failure records. See the result JSON.' }
}

function Wait-ForRedirection {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $secondary = @(Get-Process -Name MyStreamTimer -ErrorAction SilentlyContinue | Where-Object {
            $_.Id -ne $AppPid -and $_.Path -eq $executable
        })
        if ($secondary.Count -eq 0) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Activation processes did not exit: $($secondary.Id -join ', ')"
}

function Send-Protocol([string] $HostName, [string] $Query) {
    Start-Process "${Protocol}://${HostName}/?${Query}"
    $script:protocolLaunches++
}

try {
    $windows = Invoke-Ui -Arguments @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json
    $hwnd = ($windows | Where-Object { $_.title -eq 'My Stream Timer' } | Select-Object -First 1).hwnd
    if (-not $hwnd) { throw 'Main test window was not found.' }
    $welcome = Find-Element 'WelcomeBackDialog'
    if ($welcome) {
        $button = Invoke-Ui -Arguments @('search', 'Continue', '--root', 'WelcomeBackDialog', '--type', 'Button', '-w', "$hwnd", '--json') | ConvertFrom-Json
        if ($button.matchCount -ne 1) { throw 'The welcome dialog cannot be dismissed unambiguously.' }
        Invoke-Ui -Arguments @('invoke', $button.matches[0].selector, '-w', "$hwnd") | Out-Null
        Invoke-Ui -Arguments @('wait-for', 'WelcomeBackDialog', '--gone', '-w', "$hwnd", '-t', '3000') | Out-Null
    }

    $navigation = @('NavHome', 'NavCountdown1', 'NavCountdown2', 'NavCountdown3', 'NavCountdown4',
        'NavCountUp1', 'NavCountUp2', 'NavCurrentTime', 'NavAutomation', 'NavAbout', 'SettingsItem')
    $timerPages = @('NavCountdown1', 'NavCountdown2', 'NavCountdown3', 'NavCountUp1')
    $controls = @('StartStopButton', 'PauseResumeButton', 'ResetButton', 'AddMinuteButton', 'SubtractMinuteButton')
    $hosts = @('countdown', 'countdown2', 'countdown3', 'countdown4', 'countup', 'countup2', 'time', 'unknown')
    $queries = @('mins=1', 'secs=2', 'pause', 'resume', 'reset', 'stop', 'addmins=1', 'subtractsecs=30',
        'mins=1e100', 'mins=NaN', 'secs=Infinity', 'subtractmins=1', 'mins=-1', 'unknown=1')

    for ($step = 0; $step -lt $Iterations; $step++) {
        $category = $random.Next(6)
        $detail = ''
        switch ($category) {
            0 {
                $detail = $navigation[$random.Next($navigation.Count)]
                Navigate $detail
            }
            1 {
                $page = $timerPages[$random.Next($timerPages.Count)]
                $control = $controls[$random.Next($controls.Count)]
                $detail = "$page / $control"
                Navigate $page
                Invoke-Enabled $control
            }
            2 {
                $page = $timerPages[$random.Next(3)]
                Navigate $page
                $element = Find-Element 'MinutesBox'
                if ($element -and $element.isEnabled) {
                    $minutes = @(0, 1, 5, 999, 1000)[$random.Next(5)]
                    $seconds = $random.Next(60)
                    $detail = "$page / duration=$minutes m $seconds s"
                    Invoke-Ui -Arguments @('set-value', 'MinutesBox', "$minutes", '-w', "$hwnd") | Out-Null
                    Invoke-Ui -Arguments @('set-value', 'SecondsBox', "$seconds", '-w', "$hwnd") | Out-Null
                } else {
                    $detail = "$page / duration editing unavailable while running or paused"
                    $disabledActions++
                }
            }
            3 {
                Navigate 'NavHome'
                $control = @('DashCountdownStartStop', 'DashCountdown2StartStop', 'DashCountdown3StartStop',
                    'DashCountupStartStop', 'DashStopAll')[$random.Next(5)]
                $detail = "dashboard / $control"
                Invoke-Enabled $control
            }
            4 {
                Navigate 'SettingsItem'
                $theme = @('ThemeSystem', 'ThemeLight', 'ThemeDark')[$random.Next(3)]
                $detail = "settings / $theme"
                Invoke-Ui -Arguments @('invoke', $theme, '-w', "$hwnd") | Out-Null
            }
            5 {
                Navigate 'NavAutomation'
                $value = @(0, 1, 2, 60, 1000)[$random.Next(5)]
                $detail = "automation / start minutes=$value"
                Invoke-Ui -Arguments @('set-value', 'BuilderValueNumberBox', "$value", '-w', "$hwnd") | Out-Null
                Invoke-Enabled 'RunGeneratedUrlButton'
            }
        }
        $steps.Add([ordered]@{ step = $step; category = $category; action = $detail })
        if (($step + 1) % 20 -eq 0) {
            for ($index = 0; $index -lt 10; $index++) {
                Send-Protocol $hosts[$random.Next($hosts.Count)] $queries[$random.Next($queries.Count)]
            }
            Wait-ForRedirection
            Check-Health
            Write-Output "seed=$Seed; completed=$($step + 1)/$Iterations; uiCalls=$uiCalls; protocolLaunches=$protocolLaunches"
        }
    }

    Wait-ForRedirection
    Navigate 'NavHome'
    Invoke-Enabled 'DashStopAll'
    Invoke-Ui -Arguments @('wait-for', 'DashboardSummary', '--value', 'All timers are idle', '--contains', '-w', "$hwnd", '-t', '5000') | Out-Null
    Invoke-Ui -Arguments @('screenshot', 'MainNavigation', '-w', "$hwnd", '-o', (Join-Path $resultsPath 'dashboard.png')) | Out-Null
    Navigate 'SettingsItem'
    Invoke-Ui -Arguments @('invoke', 'ThemeSystem', '-w', "$hwnd") | Out-Null
    Navigate 'NavAbout'
    Invoke-Ui -Arguments @('scroll', 'MainNavigation', '--wheel', '-6', '-w', "$hwnd") | Out-Null
    Invoke-Ui -Arguments @('screenshot', 'MainNavigation', '-w', "$hwnd", '-o', (Join-Path $resultsPath 'about.png')) | Out-Null
    Check-Health
} catch {
    $failure = $_.Exception.ToString()
    Write-Output "FAIL: seed=$Seed; completed=$($steps.Count); $failure"
} finally {
    $summary = [ordered]@{
        seed = $Seed; requestedIterations = $Iterations; completedIterations = $steps.Count
        uiCalls = $uiCalls; protocolLaunchAttempts = $protocolLaunches; disabledActionsSkipped = $disabledActions
        elapsedSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2)
        package = $PackageName; primaryPid = $AppPid; os = [Environment]::OSVersion.Version.ToString()
        passed = $null -eq $failure; failure = $failure; faults = @($faults.Values); steps = $steps
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resultsPath 'results.json') -Encoding utf8
}
if ($failure) { exit 1 }
Write-Output "PASS: seed=$Seed; iterations=$($steps.Count); uiCalls=$uiCalls; protocolLaunchAttempts=$protocolLaunches; faults=$($faults.Count)"
