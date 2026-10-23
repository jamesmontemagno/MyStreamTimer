function Get-StoreReleaseNotes {
    param([string] $ReleaseNotes, [string] $SavedReleaseNotes, [bool] $RequireNotes)

    $notes = if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) { "$SavedReleaseNotes".Trim() } else { $ReleaseNotes.Trim() }
    if ($notes.Length -gt 1500) { throw "What's new must be at most 1,500 characters." }
    if ($RequireNotes -and -not $notes) {
        throw "Enter What's new before submitting to production; this run has no saved release notes."
    }
    return $notes
}

function ConvertTo-StoreReleaseHashtable {
    param([object] $InputObject)

    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $result = @{}
        foreach ($key in $InputObject.Keys) {
            $result[$key] = ConvertTo-StoreReleaseHashtable -InputObject $InputObject[$key]
        }
        return $result
    }
    if ($InputObject -is [pscustomobject]) {
        $result = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $result[$property.Name] = ConvertTo-StoreReleaseHashtable -InputObject $property.Value
        }
        return $result
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $result = @(
            foreach ($item in $InputObject) {
                ConvertTo-StoreReleaseHashtable -InputObject $item
            }
        )
        return ,$result
    }
    return $InputObject
}

function Get-StoreReleasePlan {
    param(
        [string] $EventName,
        [string] $RefName,
        [string] $Tag,
        [string] $Destination = 'flight',
        [string] $SourceRunId,
        [bool] $Publish,
        [string] $ReleaseNotes
    )

    if ($EventName -notin @('push', 'workflow_dispatch')) {
        throw 'Store releases require a Windows tag push or a manual workflow run.'
    }
    if ($EventName -eq 'push') {
        $Destination = 'flight'
        $Tag = $RefName
        $Publish = $true
        if ($SourceRunId) { throw 'Tag releases cannot reuse another run.' }
    }
    if ($Destination -cnotin @('flight', 'production')) {
        throw 'Select flight or production explicitly. An invalid destination never falls back to production.'
    }
    $SourceRunId = "$SourceRunId".Trim()
    $ReleaseNotes = Get-StoreReleaseNotes -ReleaseNotes $ReleaseNotes

    $version = ''
    if ($SourceRunId) {
        if ($SourceRunId -notmatch '^[1-9][0-9]*$') { throw 'The source run ID must be a positive integer.' }
        if ($Destination -ne 'production' -or -not $Publish) {
            throw 'Reusing a previous run requires destination=production and publish=true.'
        }
        if (-not [string]::IsNullOrWhiteSpace($Tag)) {
            throw 'Leave tag empty when promoting a previous run; its version is already built into the package.'
        }
    } else {
        if ($Tag -notmatch '^v(?<base>\d+\.\d+\.\d+)(?:\.(?<revision>\d+))?-windows$') {
            throw "Windows release tags must look like v3.2.1-windows or v3.2.1.1-windows. Got '$Tag'."
        }
        $revision = if ($Matches.revision) { $Matches.revision } else { '0' }
        $version = "$($Matches.base).$revision"
        if (@($version.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count) {
            throw 'Each package version component must be between 0 and 65535.'
        }
        $null = Get-StoreReleaseNotes -ReleaseNotes $ReleaseNotes -RequireNotes ($Destination -eq 'production' -and $Publish)
    }
    @{
        Destination = $Destination
        SourceRunId = $SourceRunId
        PackageVersion = $version
        Publish = $Publish
    }
}

function Test-StoreReleaseSource {
    param(
        [System.Collections.IDictionary] $Run,
        [array] $Artifacts,
        [string] $RunId,
        [string] $Repository,
        [string] $DefaultBranch
    )

    if ($Run.id -ne $RunId -or $Run.status -ne 'completed' -or $Run.conclusion -ne 'success' -or
        $Run.path -ne '.github/workflows/windows-store-publish.yml' -or
        $Run.event -notin @('push', 'workflow_dispatch') -or
        $Run.repository.full_name -ne $Repository -or $Run.head_repository.full_name -ne $Repository -or
        ($Run.head_branch -ne $DefaultBranch -and $Run.head_branch -notmatch '^v\d+\.\d+\.\d+(?:\.\d+)?-windows$') -or
        $Run.head_sha -notmatch '^[a-fA-F0-9]{40}$') {
        throw 'Promotion requires a successful Store Publish run from this repository on its default branch or a Windows release tag.'
    }
    $packages = @($Artifacts | Where-Object name -EQ 'windows-store-package')
    if ($packages.Count -ne 1 -or $packages[0].expired -ne $false -or
        $packages[0].workflow_run.id -ne $RunId -or $packages[0].workflow_run.head_sha -ne $Run.head_sha) {
        throw 'The source run must retain exactly one unexpired windows-store-package artifact matching its commit.'
    }
}

function Set-StoreSubmissionReleaseNotes {
    param(
        [System.Collections.IDictionary] $Submission,
        [string] $ReleaseNotes
    )

    $ReleaseNotes = "$ReleaseNotes".Trim()
    if (-not $ReleaseNotes -or $ReleaseNotes.Length -gt 1500) {
        throw "What's new must contain between 1 and 1,500 characters."
    }
    if ($Submission.listings -isnot [System.Collections.IDictionary]) {
        throw 'The production submission has no listing dictionary.'
    }
    $english = @($Submission.listings.Keys | Where-Object { $_ -match '^en(?:-|$)' })
    if ($english.Count -eq 0) { throw 'No existing English Store listing was found; no release notes were changed.' }
    foreach ($language in $english) {
        $listing = $Submission.listings[$language]
        if ($listing.baseListing -isnot [System.Collections.IDictionary]) {
            throw "The $language Store listing has no base listing."
        }
    }
    foreach ($language in $english) {
        $Submission.listings[$language].baseListing.releaseNotes = $ReleaseNotes
        foreach ($override in $Submission.listings[$language].platformOverrides.Values) {
            if ($override -is [System.Collections.IDictionary] -and $override.Contains('releaseNotes')) {
                $override.releaseNotes = $ReleaseNotes
            }
        }
    }
}
