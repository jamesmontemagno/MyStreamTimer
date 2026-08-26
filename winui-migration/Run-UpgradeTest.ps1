# One-shot ELEVATED helper for the steps that need admin (P7-2/P7-3/P7-4 in the plan).
# Run from an Administrator PowerShell:  .\winui-migration\Run-UpgradeTest.ps1 [-SkipWack] [-Restore]
#
#   1. Trusts the dev publisher certificate (subject CN=Refractored LLC, generated from the built manifest)
#   2. Runs the Windows App Certification Kit against the x64 package   (report -> artifacts\wack.xml)
#   3. Installs the 3.0 x64 .msix OVER the Store build 2.6.2 (same identity => in-place upgrade)
#   -Restore  : removes the sideloaded 3.0 and reinstalls the Store build so the machine is back to normal
param([switch]$SkipWack, [switch]$Restore)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run this from an elevated (Administrator) PowerShell.' }

if ($Restore) {
    Write-Host 'Removing sideloaded 3.0 and reinstalling the Store build...' -ForegroundColor Cyan
    Get-AppxPackage 23875RefractoredLLC.MyStreamTimer | Remove-AppxPackage
    winget install --id 9N5NXX3WK7K7 --source msstore --accept-package-agreements --accept-source-agreements
    return
}

$cert = Join-Path $root 'artifacts\refractored-dev.pfx'
$msix = Join-Path $root 'artifacts\MyStreamTimer_3.0.0.0_x64.msix'
$unsigned = Join-Path $root 'artifacts\bundle\MyStreamTimer_3.0.0.0_x64.msix'
foreach ($f in $cert, $msix) { if (-not (Test-Path $f)) { throw "Missing $f — build Release x64 (-p:MyStreamTimerStoreBuild=true) and run 'winapp package ... --cert artifacts\refractored-dev.pfx' first (see plan Resume section)." } }

Write-Host "`n[1/3] Trusting dev certificate" -ForegroundColor Cyan
winapp cert install $cert

if (-not $SkipWack -and (Test-Path $unsigned)) {
    Write-Host "`n[2/3] Windows App Certification Kit (this takes several minutes)" -ForegroundColor Cyan
    $appcert = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe"
    & $appcert reset | Out-Null
    $report = Join-Path $root 'artifacts\wack.xml'
    & $appcert test -appxpackagepath $unsigned -reportoutputpath $report
    $xml = [xml](Get-Content $report)
    $overall = $xml.REPORT.OVERALL_RESULT
    Write-Host "WACK overall result: $overall" -ForegroundColor ($(if ($overall -eq 'PASS') { 'Green' } else { 'Yellow' }))
    $xml.SelectNodes('//TEST[RESULT!="PASS"]') | ForEach-Object { "  $($_.RESULT): $($_.NAME) — $($_.DESCRIPTION)" }
} else { Write-Host "`n[2/3] WACK skipped" -ForegroundColor DarkGray }

Write-Host "`n[3/3] Installing 3.0 over the Store build (in-place upgrade)" -ForegroundColor Cyan
$before = Get-AppxPackage 23875RefractoredLLC.MyStreamTimer
Write-Host "  before: $($before.Version)  PFN=$($before.PackageFamilyName)"
Add-AppxPackage -Path $msix
$after = Get-AppxPackage 23875RefractoredLLC.MyStreamTimer
Write-Host "  after : $($after.Version)  PFN=$($after.PackageFamilyName)"
if ($before.PackageFamilyName -ne $after.PackageFamilyName) { throw 'PFN changed — this was NOT an in-place upgrade!' }

Write-Host "`nNow launch the app and walk winui-migration\upgrade-test-checklist.md." -ForegroundColor Green
Write-Host "Settings should already be there; output folder = $env:LOCALAPPDATA\Packages\$($after.PackageFamilyName)\LocalState\ProgramData\MyStreamTimer"
Write-Host "When done:  .\winui-migration\Run-UpgradeTest.ps1 -Restore"
