$ErrorActionPreference = 'Stop'
$resolver = Join-Path $PSScriptRoot 'Resolve-StoreFlight.ps1'
$flightA = @{ flightId = 'flight-a'; friendlyName = 'IAP Preview'; groupIds = @('group-1') }
$flightB = @{ flightId = 'flight-b'; friendlyName = 'Other Preview'; groupIds = @('group-2') }
$nextPage = 'applications/local-product/listflights/?skip=1&top=100'

function New-Page($Flights, [long]$Count, [string]$NextLink) {
    $page = @{ value = @($Flights); totalCount = $Count }
    if ($NextLink) { $page['@nextLink'] = $NextLink }
    $page | ConvertTo-Json -Compress -Depth 6
}

$one = New-Page @($flightA) 1
$two = New-Page @($flightA, $flightB) 2
$pageA = New-Page @($flightA) 2 $nextPage
$pageB = New-Page @($flightB) 2
$ready = $flightA | ConvertTo-Json -Compress
$pending = @{ flightId = 'flight-a'; pendingFlightSubmission = @{ id = 'draft-1' } } | ConvertTo-Json -Compress -Depth 4
function New-Draft([string]$Status = 'PendingCommit', [string]$UploadUrl = '', [string]$Flight = 'flight-a') {
    @{ id = 'draft-1'; flightId = $Flight; status = $Status; fileUploadUrl = $UploadUrl } | ConvertTo-Json -Compress
}
$created = @{ flightId = 'flight-a'; friendlyName = 'Internal Testing' } | ConvertTo-Json -Compress
$cases = @(
    @{ Name = 'only flight'; Replies = @($one); Selected = 'flight-a' }
    @{ Name = 'whitespace override'; Replies = @($one); Override = '   '; Selected = 'flight-a' }
    @{ Name = 'trimmed override'; Replies = @($one); Override = ' flight-a '; Selected = 'flight-a' }
    @{ Name = 'explicit choice'; Replies = @($two); Override = 'flight-b'; Selected = 'flight-b' }
    @{ Name = 'ambiguous flights'; Replies = @($two); Error = 'Multiple package flights' }
    @{ Name = 'no flights'; Replies = @((New-Page @() 0)); Error = 'No package flights' }
    @{ Name = 'HTTP 404'; Replies = @(404); Error = 'No package flights' }
    @{ Name = 'HTTP 401'; Replies = @(401); Error = 'HTTP 401' }
    @{ Name = 'HTTP 403'; Replies = @(403); Error = 'HTTP 403' }
    @{ Name = 'HTTP 500'; Replies = @(500); Error = 'HTTP 500' }
    @{ Name = 'network error'; Replies = @('network-failure'); Error = 'simulated network failure' }
    @{ Name = 'unknown override'; Replies = @($one); Override = 'unknown-flight'; Error = 'does not match' }
    @{ Name = 'missing count'; Replies = @('{"value":[]}'); Error = 'invalid response' }
    @{ Name = 'string count'; Replies = @('{"value":[],"totalCount":"0"}'); Error = 'invalid response' }
    @{ Name = 'negative count'; Replies = @('{"value":[],"totalCount":-1}'); Error = 'invalid response' }
    @{ Name = 'non-array flights'; Replies = @('{"value":{},"totalCount":1}'); Error = 'invalid response' }
    @{ Name = 'missing flights'; Replies = @('{"totalCount":0}'); Error = 'invalid response' }
    @{ Name = 'null response'; Replies = @('null'); Error = 'invalid response' }
    @{ Name = 'blank flight ID'; Replies = @('{"value":[{"flightId":""}],"totalCount":1}'); Error = 'invalid or duplicate flight ID' }
    @{ Name = 'newline flight ID'; Replies = @('{"value":[{"flightId":"flight\nid"}],"totalCount":1}'); Error = 'invalid or duplicate flight ID' }
    @{ Name = 'duplicate ID'; Replies = @((New-Page @($flightA, $flightA) 2)); Error = 'invalid or duplicate flight ID' }
    @{ Name = 'paginated ambiguity'; Replies = @($pageA, $pageB); Error = 'Multiple package flights'; Requests = 2 }
    @{ Name = 'paginated explicit choice'; Replies = @($pageA, $pageB); Override = 'flight-b'; Selected = 'flight-b'; Requests = 2 }
    @{ Name = 'incomplete response'; Replies = @((New-Page @($flightA) 2)); Error = 'incomplete list' }
    @{ Name = 'changing list'; Replies = @($pageA, (New-Page @($flightB) 3)); Error = 'changed during discovery'; Requests = 2 }
    @{ Name = 'repeated page'; Replies = @((New-Page @($flightA) 2 'applications/local-product/listflights?top=100')); Error = 'repeated page' }
    @{ Name = 'foreign next host'; Replies = @((New-Page @($flightA) 2 'https://example.invalid/listflights')); Error = 'unexpected next page' }
    @{ Name = 'other app next page'; Replies = @((New-Page @($flightA) 2 'applications/other-product/listflights')); Error = 'unexpected next page' }
    @{ Name = 'invalid next page type'; Replies = @('{"value":[{"flightId":"flight-a"}],"totalCount":2,"@nextLink":1}'); Error = 'invalid next page' }
    @{ Name = 'inconsistent count'; Replies = @((New-Page @($flightA, $flightB) 1)); Error = 'inconsistent count' }
    @{ Name = 'empty page with continuation'; Replies = @((New-Page @() 1 $nextPage)); Error = 'invalid next page' }
    @{ Name = 'read-only with groups'; Replies = @((New-Page @() 0)); Groups = 'group-1'; Error = 'No package flights' }
    @{ Name = 'create API flight'; Replies = @((New-Page @() 0), $created, $ready); Prepare = $true; Groups = ' group-1,group-2,group-1 '; Selected = 'flight-a'; Methods = @('Get', 'Post', 'Get'); Requests = 3 }
    @{ Name = 'missing groups'; Replies = @((New-Page @() 0)); Prepare = $true; Error = 'MICROSOFT_STORE_FLIGHT_GROUP_IDS' }
    @{ Name = 'invalid groups'; Replies = @((New-Page @() 0)); Prepare = $true; Groups = 'group-1,,group-2'; Error = 'MICROSOFT_STORE_FLIGHT_GROUP_IDS' }
    @{ Name = 'override never creates flight'; Replies = @((New-Page @() 0)); Prepare = $true; Override = 'missing'; Groups = 'group-1'; Error = 'No package flights' }
    @{ Name = 'creation failure'; Replies = @((New-Page @() 0), 403); Prepare = $true; Groups = 'group-1'; Error = 'simulated HTTP failure'; Methods = @('Get', 'Post'); Requests = 2 }
    @{ Name = 'invalid creation response'; Replies = @((New-Page @() 0), $ready); Prepare = $true; Groups = 'group-1'; Error = 'unexpected created flight'; Methods = @('Get', 'Post'); Requests = 2 }
    @{ Name = 'prepare existing'; Replies = @($one, $ready); Prepare = $true; Selected = 'flight-a'; Requests = 2 }
    @{ Name = 'API draft preserved'; Replies = @($one, $pending, (New-Draft -UploadUrl 'https://upload.invalid')); Prepare = $true; Replace = $true; Selected = 'flight-a'; Requests = 3 }
    @{ Name = 'portal draft needs consent'; Replies = @($one, $pending, (New-Draft)); Prepare = $true; Error = 'portal-created draft'; Requests = 3 }
    @{ Name = 'replace approved portal draft'; Replies = @($one, $pending, (New-Draft), 'null'); Prepare = $true; Replace = $true; Selected = 'flight-a'; Methods = @('Get', 'Get', 'Get', 'Delete'); Requests = 4 }
    @{ Name = 'processing submission preserved'; Replies = @($one, $pending, (New-Draft -Status 'Certification')); Prepare = $true; Replace = $true; Error = 'not editable'; Requests = 3 }
    @{ Name = 'failed portal draft preserved'; Replies = @($one, $pending, (New-Draft -Status 'CommitFailed')); Prepare = $true; Replace = $true; Error = 'portal-created draft'; Requests = 3 }
    @{ Name = 'unexpected submission preserved'; Replies = @($one, $pending, (New-Draft -Flight 'flight-b')); Prepare = $true; Replace = $true; Error = 'unexpected flight submission'; Requests = 3 }
    @{ Name = 'draft deletion failure'; Replies = @($one, $pending, (New-Draft), 403); Prepare = $true; Replace = $true; Error = 'simulated HTTP failure'; Methods = @('Get', 'Get', 'Get', 'Delete'); Requests = 4 }
    @{ Name = 'read-only replacement prohibited'; Replies = @(); Replace = $true; Error = 'requires flight preparation'; Requests = 0 }
)

# Shadow the HTTP cmdlet so these tests cannot contact Partner Center.
function Invoke-RestMethod {
    [CmdletBinding()]
    param([string]$Method, [uri]$Uri, [hashtable]$Headers, [int]$TimeoutSec, [string]$ContentType, $Body)
    $index = $global:FlightSelectionTestRequests.Count
    $expectedMethod = if ($case.Methods) { $case.Methods[$index] } else { 'Get' }
    if ($Method -ne $expectedMethod -or $TimeoutSec -ne 60 -or
        $Headers.Authorization -ne 'Bearer local-flight-token' -or
        $Uri.Host -ne 'manage.devcenter.microsoft.com') {
        throw 'Unexpected authenticated HTTP request.'
    }
    if ($Method -eq 'Post') {
        $payload = $Body | ConvertFrom-Json
        $expectedGroups = if ($case.Name -eq 'create API flight') { 'group-1,group-2' } else { 'group-1' }
        if ($Uri.AbsolutePath -ne '/v1.0/my/applications/local-product/flights' -or
            $ContentType -ne 'application/json' -or $payload.friendlyName -cne 'Internal Testing' -or
            ($payload.groupIds -join ',') -ne $expectedGroups) {
            throw 'Unexpected flight creation payload.'
        }
    }
    if ($Method -eq 'Delete' -and $Uri.AbsolutePath -ne '/v1.0/my/applications/local-product/flights/flight-a/submissions/draft-1') {
        throw 'Deletion targeted something other than the approved flight draft.'
    }
    $global:FlightSelectionTestRequests.Add($Uri.AbsoluteUri)
    if ($global:FlightSelectionTestReplies.Count -eq 0) {
        throw 'Unexpected additional HTTP request.'
    }
    $reply = $global:FlightSelectionTestReplies.Dequeue()
    if ($reply -is [int]) {
        $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]$reply)
        throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('simulated HTTP failure', $response)
    }
    if ($reply -eq 'network-failure') {
        throw [System.Net.Http.HttpRequestException]::new('simulated network failure')
    }
    $reply | ConvertFrom-Json
}
if ((Get-Command Invoke-RestMethod).CommandType -ne 'Function') {
    throw 'The non-networking HTTP replacement is not active.'
}

try {
    foreach ($case in $cases) {
        $global:FlightSelectionTestReplies = [System.Collections.Generic.Queue[object]]::new()
        foreach ($reply in $case.Replies) { $global:FlightSelectionTestReplies.Enqueue($reply) }
        $global:FlightSelectionTestRequests = [System.Collections.Generic.List[string]]::new()
        $failure = $null
        $selection = @()
        try {
            $selection = @(& $resolver -ProductId 'local-product' -AccessToken 'local-flight-token' -FlightId $case.Override `
                -FlightGroupIds $case.Groups -PrepareFlight:([bool]$case.Prepare) -ReplaceFlightDraft:([bool]$case.Replace))
        }
        catch { $failure = $_.Exception.Message }
        if ($case.Error) {
            if (-not $failure -or $failure -notlike "*$($case.Error)*" -or $selection.Count -ne 0) {
                throw "FAIL: $($case.Name): expected '$($case.Error)' with no selection; got '$failure', selection '$selection'."
            }
        } elseif ($failure -or $selection.Count -ne 1 -or $selection[0] -ne $case.Selected) {
            throw "FAIL: $($case.Name): expected '$($case.Selected)'; got '$failure', selection '$selection'."
        }
        $expectedRequests = if ($case.ContainsKey('Requests')) { $case.Requests } else { 1 }
        if ($global:FlightSelectionTestRequests.Count -ne $expectedRequests -or $global:FlightSelectionTestReplies.Count -ne 0) {
            throw "FAIL: $($case.Name): unexpected pagination/request count."
        }
        Write-Host "PASS: $($case.Name)"
    }
    Write-Host "$($cases.Count) flight-selection cases passed. No real HTTP requests or Store changes were made."
} finally {
    Remove-Variable FlightSelectionTestReplies, FlightSelectionTestRequests -Scope Global -ErrorAction SilentlyContinue
}
