[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('flight', 'production')][string] $Destination,
    [Parameter(Mandatory)][ValidatePattern('\.msixupload$')][string] $PackagePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9]+$')][string] $ProductId,
    [string] $FlightId,
    [string] $ReleaseNotes
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'StoreRelease.ps1')

$arguments = @('publish', $PackagePath, '--appId', $ProductId, '--uploadTimeout', '900')
if ($Destination -eq 'flight') {
    if ($FlightId -notmatch '^[a-zA-Z0-9-]+$') {
        throw 'No valid package flight was resolved. Production publishing is never a fallback.'
    }
    $arguments += @('--flightId', $FlightId)
} else {
    if ($FlightId) { throw 'Production publishing must not specify a flight ID.' }
    $ReleaseNotes = Get-StoreReleaseNotes -ReleaseNotes $ReleaseNotes -RequireNotes $true
    $token = Invoke-RestMethod -Method Post `
        -Uri "https://login.microsoftonline.com/$env:PARTNER_CENTER_TENANT_ID/oauth2/token" `
        -ContentType 'application/x-www-form-urlencoded' -TimeoutSec 60 `
        -Body @{
            grant_type = 'client_credentials'
            client_id = $env:PARTNER_CENTER_CLIENT_ID
            client_secret = $env:PARTNER_CENTER_CLIENT_SECRET
            resource = 'https://manage.devcenter.microsoft.com'
        }
    if ([string]::IsNullOrWhiteSpace($token.access_token)) { throw 'Partner Center returned no access token.' }
    $headers = @{ Authorization = "Bearer $($token.access_token)" }
    $appUri = "https://manage.devcenter.microsoft.com/v1.0/my/applications/$ProductId"
    $application = Invoke-RestMethod -Method Get -Uri $appUri -Headers $headers -TimeoutSec 60
    if ($application.pendingApplicationSubmission.id) {
        throw 'This app already has a pending production submission. Review or remove it in Partner Center before retrying; it was not overwritten.'
    }
    $arguments += '--noCommit'
}

# Always upload the complete .msixupload, including its matching .appxsym archives.
& msstore @arguments
if ($LASTEXITCODE -ne 0) { throw "msstore $Destination package upload failed." }
if ($Destination -eq 'flight') { return }

$application = Invoke-RestMethod -Method Get -Uri $appUri -Headers $headers -TimeoutSec 60
$submissionId = $application.pendingApplicationSubmission.id
if ($submissionId -notmatch '^[a-zA-Z0-9-]+$') { throw 'The uploaded production draft could not be identified. Nothing was committed.' }
$submissionUri = "$appUri/submissions/$submissionId"
$submission = ConvertTo-StoreReleaseHashtable (Invoke-RestMethod -Method Get -Uri $submissionUri -Headers $headers -TimeoutSec 60)
if ($submission.id -ne $submissionId -or $submission.status -ne 'PendingCommit') {
    throw 'The production draft is not awaiting commit. Nothing was committed.'
}
Set-StoreSubmissionReleaseNotes -Submission $submission -ReleaseNotes $ReleaseNotes
$json = $submission | ConvertTo-Json -Depth 100 -Compress
Invoke-RestMethod -Method Put -Uri $submissionUri -Headers $headers -TimeoutSec 60 `
    -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json)) | Out-Null

$saved = ConvertTo-StoreReleaseHashtable (Invoke-RestMethod -Method Get -Uri $submissionUri -Headers $headers -TimeoutSec 60)
foreach ($language in @($submission.listings.Keys | Where-Object { $_ -match '^en(?:-|$)' })) {
    if ($saved.listings[$language].baseListing.releaseNotes -cne $ReleaseNotes.Trim()) {
        throw "The $language What's new text was not saved. The production submission remains uncommitted."
    }
    foreach ($platform in $submission.listings[$language].platformOverrides.Keys) {
        $override = $submission.listings[$language].platformOverrides[$platform]
        if ($override -is [System.Collections.IDictionary] -and $override.Contains('releaseNotes') -and
            $saved.listings[$language].platformOverrides[$platform].releaseNotes -cne $ReleaseNotes) {
            throw "The $language/$platform What's new text was not saved. Nothing was committed."
        }
    }
}
if ($saved.id -ne $submissionId -or $saved.status -ne 'PendingCommit') {
    throw 'The production draft changed before commit. Nothing was committed.'
}
$commit = Invoke-RestMethod -Method Post -Uri "$submissionUri/commit" -Headers $headers -TimeoutSec 60
if ($commit.status -ne 'CommitStarted') {
    throw "Partner Center did not confirm the production commit (status '$($commit.status)'). Check the submission before retrying."
}
Write-Host "Production submission $submissionId committed with English release notes. Certification and the submission's release settings still apply."
