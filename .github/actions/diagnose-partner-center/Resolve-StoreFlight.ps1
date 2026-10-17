[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$AccessToken,
    [string]$FlightId
)

$ErrorActionPreference = 'Stop'
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
            throw 'No package flights were found. In Partner Center, create one package flight linked to the IAP Test group, then reuse that flight for future releases.'
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
    throw 'No package flights were found. In Partner Center, create one package flight linked to the IAP Test group, then reuse that flight for future releases.'
}

foreach ($flight in $flights) {
    Write-Host "Package flight: $($flight.friendlyName); ID=$($flight.flightId); group IDs=$($flight.groupIds -join ', ')"
}
$requestedId = "$FlightId".Trim()
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

Write-Host "Selected package flight: $($selected.friendlyName); ID=$($selected.flightId)"
$selected.flightId
