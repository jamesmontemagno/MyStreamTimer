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
)

# Shadow the HTTP cmdlet so these tests cannot contact Partner Center.
function Invoke-RestMethod {
    [CmdletBinding()]
    param([string]$Method, [uri]$Uri, [hashtable]$Headers, [int]$TimeoutSec)
    if ($Method -ne 'Get' -or $TimeoutSec -ne 60 -or
        $Headers.Authorization -ne 'Bearer local-flight-token' -or
        $Uri.Host -ne 'manage.devcenter.microsoft.com') {
        throw 'Unexpected authenticated HTTP request.'
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
            $selection = @(& $resolver -ProductId 'local-product' -AccessToken 'local-flight-token' -FlightId $case.Override)
        }
        catch { $failure = $_.Exception.Message }
        if ($case.Error) {
            if (-not $failure -or $failure -notlike "*$($case.Error)*" -or $selection.Count -ne 0) {
                throw "FAIL: $($case.Name): expected '$($case.Error)' with no selection; got '$failure', selection '$selection'."
            }
        } elseif ($failure -or $selection.Count -ne 1 -or $selection[0] -ne $case.Selected) {
            throw "FAIL: $($case.Name): expected '$($case.Selected)'; got '$failure', selection '$selection'."
        }
        $expectedRequests = if ($case.Requests) { $case.Requests } else { 1 }
        if ($global:FlightSelectionTestRequests.Count -ne $expectedRequests -or $global:FlightSelectionTestReplies.Count -ne 0) {
            throw "FAIL: $($case.Name): unexpected pagination/request count."
        }
        Write-Host "PASS: $($case.Name)"
    }
    Write-Host "$($cases.Count) flight-selection cases passed. No real HTTP requests or Store changes were made."
} finally {
    Remove-Variable FlightSelectionTestReplies, FlightSelectionTestRequests -Scope Global -ErrorAction SilentlyContinue
}
