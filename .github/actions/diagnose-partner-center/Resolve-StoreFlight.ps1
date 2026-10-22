[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$AccessToken,
    [string]$FlightId,
    [string]$FlightGroupIds,
    [switch]$PrepareFlight,
    [switch]$ReplaceFlightDraft
)

$ErrorActionPreference = 'Stop'
if ($ReplaceFlightDraft -and -not $PrepareFlight) {
    throw 'Draft replacement requires flight preparation.'
}
$requestedId = "$FlightId".Trim()
$headers = @{ Authorization = "Bearer $AccessToken" }
$baseUri = [uri]'https://manage.devcenter.microsoft.com/v1.0/my/'
$flightPath = "applications/$([uri]::EscapeDataString($ProductId))/listflights"
$nextUri = [uri]::new($baseUri, "$flightPath`?top=100")
$seenPages = [System.Collections.Generic.HashSet[string]]::new()
$seenIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$flights = [System.Collections.Generic.List[object]]::new()
$totalCount = $null
do {
    if (-not $seenPages.Add($nextUri.AbsoluteUri)) {
        throw 'Package flight discovery returned a repeated page. No flight was selected.'
    }
    try {
        $page = Invoke-RestMethod -Method Get -Uri $nextUri `
            -Headers @{ Authorization = "Bearer $AccessToken" } -TimeoutSec 60
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        if ($_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::NotFound) {
            throw 'No package flights were found. Check the product ID and API access; use an approved flight publish with MICROSOFT_STORE_FLIGHT_GROUP_IDS when the flight list is empty.'
        }
        throw "Package flight discovery failed with HTTP $([int]$_.Exception.Response.StatusCode). Check this app's Partner Center API access. No flight was selected."
    }

    if ($null -eq $page -or $page.value -isnot [array] -or
        ($page.totalCount -isnot [int] -and $page.totalCount -isnot [long]) -or $page.totalCount -lt 0) {
        throw 'Package flight discovery returned an invalid response. No flight was selected.'
    }
    if ($null -eq $totalCount) {
        $totalCount = $page.totalCount
    }
    if ($page.totalCount -ne $totalCount) {
        throw 'The package flight list changed during discovery. Retry before publishing.'
    }
    foreach ($flight in $page.value) {
        if ($flight.flightId -isnot [string] -or $flight.flightId -notmatch '^[a-zA-Z0-9-]+$' -or
            -not $seenIds.Add($flight.flightId)) {
            throw 'Package flight discovery returned an invalid or duplicate flight ID. No flight was selected.'
        }
        $flights.Add($flight)
    }
    if ($flights.Count -gt $totalCount) {
        throw 'Package flight discovery returned an inconsistent count. No flight was selected.'
    }

    $nextUri = $null
    if ($page.'@nextLink') {
        if ($page.'@nextLink' -isnot [string] -or $page.value.Count -eq 0) {
            throw 'Package flight discovery returned an invalid next page. No flight was selected.'
        }
        $nextUri = [uri]::new($baseUri, $page.'@nextLink')
        if ($nextUri.GetLeftPart([UriPartial]::Authority) -ne $baseUri.GetLeftPart([UriPartial]::Authority) -or
            $nextUri.AbsolutePath.TrimEnd('/') -ne "$($baseUri.AbsolutePath)$flightPath") {
            throw 'Package flight discovery returned an unexpected next page. No flight was selected.'
        }
    }
} while ($null -ne $nextUri)

if ($flights.Count -ne $totalCount) {
    throw 'Package flight discovery returned an incomplete list. No flight was selected.'
}
if ($flights.Count -eq 0) {
    if (-not $PrepareFlight -or $requestedId) {
        throw 'No package flights were found. Use an approved flight publish with MICROSOFT_STORE_FLIGHT_GROUP_IDS to create Internal Testing, or correct MICROSOFT_STORE_FLIGHT_ID. Diagnostics never create flights.'
    }
    $groupIds = @($FlightGroupIds -split ',' | ForEach-Object { $_.Trim() } | Select-Object -Unique)
    if ($groupIds.Count -eq 0 -or @($groupIds | Where-Object { $_ -cnotmatch '\A[a-zA-Z0-9][a-zA-Z0-9-]*\z' }).Count) {
        throw 'Set MICROSOFT_STORE_FLIGHT_GROUP_IDS to comma-separated Partner Center tester group IDs before creating Internal Testing. Group names are not IDs.'
    }
    $created = Invoke-RestMethod -Method Post `
        -Uri ([uri]::new($baseUri, "applications/$([uri]::EscapeDataString($ProductId))/flights")) `
        -Headers $headers -TimeoutSec 60 -ContentType 'application/json' `
        -Body (@{ friendlyName = 'Internal Testing'; groupIds = $groupIds } | ConvertTo-Json -Depth 5 -Compress)
    if ($created.friendlyName -cne 'Internal Testing' -or
        $created.flightId -isnot [string] -or $created.flightId -cnotmatch '\A[a-zA-Z0-9][a-zA-Z0-9-]*\z') {
        throw 'Partner Center returned an unexpected created flight. Inspect flights before retrying.'
    }
    $flights.Add($created)
    Write-Host 'Created API-managed Internal Testing with the configured tester groups.'
}

foreach ($flight in $flights) {
    Write-Host "Package flight: $($flight.friendlyName); ID=$($flight.flightId); group IDs=$($flight.groupIds -join ', ')"
}
if ($requestedId) {
    $matchingFlights = @($flights | Where-Object flightId -EQ $requestedId)
    if ($matchingFlights.Count -ne 1) {
        throw 'MICROSOFT_STORE_FLIGHT_ID does not match an existing package flight for this app. No flight was selected.'
    }
    $selected = $matchingFlights[0]
} elseif ($flights.Count -eq 1) {
    $selected = $flights[0]
} else {
    throw 'Multiple package flights exist. Set MICROSOFT_STORE_FLIGHT_ID to one of the IDs listed above. No flight was selected.'
}

if ($PrepareFlight) {
    $flightUri = [uri]::new($baseUri, "applications/$([uri]::EscapeDataString($ProductId))/flights/$($selected.flightId)")
    $flight = Invoke-RestMethod -Method Get -Uri $flightUri -Headers $headers -TimeoutSec 60
    if ($flight.flightId -cne $selected.flightId) {
        throw 'Partner Center returned an unexpected flight. No draft was removed.'
    }
    if ($flight.pendingFlightSubmission) {
        $submissionId = $flight.pendingFlightSubmission.id
        if ($submissionId -isnot [string] -or $submissionId -cnotmatch '\A[a-zA-Z0-9][a-zA-Z0-9-]*\z') {
            throw 'Partner Center returned an invalid pending submission ID. No draft was removed.'
        }
        $submissionUri = "$($flightUri.AbsoluteUri)/submissions/$submissionId"
        $submission = Invoke-RestMethod -Method Get -Uri $submissionUri -Headers $headers -TimeoutSec 60
        if ($submission.id -cne $submissionId -or $submission.flightId -cne $selected.flightId) {
            throw 'Partner Center returned an unexpected flight submission. No draft was removed.'
        }
        if ($submission.status -notin @('PendingCommit', 'CommitFailed')) {
            throw 'The flight submission is processing or not editable. Wait or resolve it in Partner Center. No draft was removed.'
        }
        if ([string]::IsNullOrWhiteSpace($submission.fileUploadUrl)) {
            if (-not $ReplaceFlightDraft -or $submission.status -ne 'PendingCommit') {
                throw 'The flight has a portal-created draft without an API upload URL. Discard only the unsubmitted draft in Partner Center, or manually publish with replace_flight_draft enabled. No draft was removed.'
            }
            Invoke-RestMethod -Method Delete -Uri $submissionUri -Headers $headers -TimeoutSec 60 | Out-Null
            Write-Host 'Discarded only the explicitly approved portal draft; the flight and tester groups are preserved.'
        }
    }
}

Write-Host "Selected package flight: $($selected.friendlyName); ID=$($selected.flightId)"
$selected.flightId
