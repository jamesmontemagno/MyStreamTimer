$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'StoreRelease.ps1')
$passed = 0
[int] $previousNativeExitCode = $global:LASTEXITCODE

function Assert($Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Test-Case([string] $Name, [scriptblock] $Body, [string] $ErrorMessage) {
    $failure = $null
    try { & $Body } catch { $failure = $_.Exception.Message }
    if ($ErrorMessage) {
        Assert ($failure -and $failure -like "*$ErrorMessage*") "FAIL $Name`: expected '$ErrorMessage'; got '$failure'."
    } else {
        Assert (-not $failure) "FAIL $Name`: $failure"
    }
    $script:passed++
    Write-Host "PASS: $Name"
}

Test-Case 'tag pushes stay flight-first even with a production input' {
    $plan = Get-StoreReleasePlan -EventName push -RefName v3.2.1-windows -Destination production
    Assert ($plan.Destination -eq 'flight' -and $plan.Publish -and $plan.PackageVersion -eq '3.2.1.0') 'Incorrect tag route.'
}
foreach ($destination in @('flight', 'production')) {
    Test-Case "$destination build without submission" {
        $plan = Get-StoreReleasePlan -EventName workflow_dispatch -Tag v3.2.1.2-windows -Destination $destination
        Assert (-not $plan.Publish -and $plan.PackageVersion -eq '3.2.1.2') 'Build-only submitted or lost revision.'
    }
    Test-Case "$destination explicit submission" {
        $plan = Get-StoreReleasePlan -EventName workflow_dispatch -Tag v3.2.1-windows -Destination $destination -Publish $true -ReleaseNotes 'Timer fixes.'
        Assert ($plan.Publish -and $plan.Destination -eq $destination) 'Wrong publication route.'
    }
}
foreach ($invalid in @('', 'prod', 'Production', 'flight;production')) {
    Test-Case "reject invalid destination '$invalid'" {
        Get-StoreReleasePlan -EventName workflow_dispatch -Tag v3.2.1-windows -Destination $invalid
    } 'Select flight or production'
}
Test-Case 'production requires release notes' {
    Get-StoreReleasePlan -EventName workflow_dispatch -Tag v3.2.1-windows -Destination production -Publish $true
} "Enter What's new"
Test-Case 'oversized release notes rejected before packaging' {
    Get-StoreReleasePlan -EventName workflow_dispatch -Tag v3.2.1-windows -ReleaseNotes ('x' * 1501)
} 'at most 1,500'
Test-Case 'promotion inherits saved English release notes exactly' {
    $notes = "Fixed pause.`nImproved output."
    Assert ((Get-StoreReleaseNotes -SavedReleaseNotes $notes -RequireNotes $true) -ceq $notes) 'Saved notes were not inherited.'
}
Test-Case 'API response conversion creates nested dictionaries without changing arrays' {
    $response = [pscustomobject]@{
        listings = [pscustomobject]@{
            'en-us' = [pscustomobject]@{ baseListing = [pscustomobject]@{ releaseNotes = 'Fixes.' } }
        }
        artifacts = @([pscustomobject]@{ name = 'windows-store-package' })
    }
    $converted = ConvertTo-StoreReleaseHashtable $response
    Assert ($converted -is [System.Collections.IDictionary] -and
        $converted.listings -is [System.Collections.IDictionary] -and
        $converted.listings['en-us'] -is [System.Collections.IDictionary] -and
        $converted.listings['en-us'].baseListing -is [System.Collections.IDictionary] -and
        $converted.listings['en-us'].baseListing.releaseNotes -eq 'Fixes.') 'Nested API objects were not converted to dictionaries.'
    Assert ($converted.artifacts -is [array] -and $converted.artifacts.Count -eq 1 -and
        $converted.artifacts[0] -is [System.Collections.IDictionary] -and
        $converted.artifacts[0].name -eq 'windows-store-package') 'API arrays were changed or not recursively converted.'
}
Test-Case 'promotion can override saved notes without changing package bytes' {
    Assert ((Get-StoreReleaseNotes -ReleaseNotes ' New notes. ' -SavedReleaseNotes 'Old notes.' -RequireNotes $true) -ceq 'New notes.') 'Notes override was ignored.'
}
Test-Case 'legacy source without saved notes needs explicit notes' {
    Get-StoreReleaseNotes -RequireNotes $true
} "Enter What's new"
Test-Case 'full 1500-character notes accepted' {
    Assert ((Get-StoreReleaseNotes -ReleaseNotes ('x' * 1500) -RequireNotes $true).Length -eq 1500) 'Boundary notes rejected.'
}
foreach ($invalid in @('3.2.1', "v3.2.1-windows`nproduction", 'v65536.0.0-windows')) {
    Test-Case "reject invalid version '$invalid'" {
        Get-StoreReleasePlan -EventName workflow_dispatch -Tag $invalid
    } $(if ($invalid -like '*65536*') { 'between 0 and 65535' } else { 'Windows release tags' })
}
Test-Case 'promotion has no version stamp or build' {
    $plan = Get-StoreReleasePlan -EventName workflow_dispatch -Destination production -SourceRunId ' 123 ' -Publish $true
    Assert ($plan.SourceRunId -eq '123' -and $plan.PackageVersion -eq '') 'Promotion rebuilt or restamped its source.'
}
Test-Case 'promotion rejects an ambiguous new tag' {
    Get-StoreReleasePlan -EventName workflow_dispatch -Destination production -SourceRunId 123 -Publish $true -Tag v3.2.1-windows
} 'Leave tag empty'
Test-Case 'promotion requires production' {
    Get-StoreReleasePlan -EventName workflow_dispatch -SourceRunId 123 -Publish $true
} 'destination=production'
Test-Case 'promotion requires publish' {
    Get-StoreReleasePlan -EventName workflow_dispatch -Destination production -SourceRunId 123
} 'publish=true'
foreach ($invalid in @('0', '-1', 'abc', '123/../456', "123`nx=true")) {
    Test-Case "reject invalid source '$invalid'" {
        Get-StoreReleasePlan -EventName workflow_dispatch -Destination production -SourceRunId $invalid -Publish $true
    } 'positive integer'
}

function New-SourceRun {
    @{
        id = 123; status = 'completed'; conclusion = 'success'; event = 'workflow_dispatch'
        path = '.github/workflows/windows-store-publish.yml'; head_branch = 'main'; head_sha = 'a' * 40
        repository = @{ full_name = 'owner/repo' }; head_repository = @{ full_name = 'owner/repo' }
    }
}
function New-PackageArtifact {
    @{ name = 'windows-store-package'; expired = $false; workflow_run = @{ id = 123; head_sha = 'a' * 40 } }
}
foreach ($branch in @('main', 'v3.2.1-windows')) {
    Test-Case "accept successful source from $branch" {
        $run = New-SourceRun
        $run.head_branch = $branch
        Test-StoreReleaseSource -Run $run -Artifacts @((New-PackageArtifact)) -RunId 123 -Repository owner/repo -DefaultBranch main
    }
}
foreach ($change in @(
    @{ status = 'in_progress' }, @{ conclusion = 'failure' }, @{ conclusion = 'cancelled' },
    @{ event = 'pull_request' }, @{ path = '.github/workflows/ci.yml' }, @{ head_branch = 'unreviewed-branch' },
    @{ head_sha = 'invalid' }, @{ id = 456 }, @{ repository = @{ full_name = 'other/repo' } },
    @{ head_repository = @{ full_name = 'fork/repo' } }
)) {
    Test-Case "reject untrusted/unsuccessful source $($change.Keys)" {
        $run = New-SourceRun
        foreach ($key in $change.Keys) { $run[$key] = $change[$key] }
        Test-StoreReleaseSource -Run $run -Artifacts @((New-PackageArtifact)) -RunId 123 -Repository owner/repo -DefaultBranch main
    } 'successful Store Publish run'
}
foreach ($change in @(@{ expired = $true }, @{ workflow_run = @{ id = 456; head_sha = 'a' * 40 } }, @{ workflow_run = @{ id = 123; head_sha = 'b' * 40 } })) {
    Test-Case "reject source artifact $($change.Keys)" {
        $artifact = New-PackageArtifact
        foreach ($key in $change.Keys) { $artifact[$key] = $change[$key] }
        Test-StoreReleaseSource -Run (New-SourceRun) -Artifacts @($artifact) -RunId 123 -Repository owner/repo -DefaultBranch main
    } 'exactly one unexpired'
}
foreach ($artifacts in @(@(), @((New-PackageArtifact), (New-PackageArtifact)))) {
    Test-Case "reject $($artifacts.Count) matching artifacts" {
        Test-StoreReleaseSource -Run (New-SourceRun) -Artifacts $artifacts -RunId 123 -Repository owner/repo -DefaultBranch main
    } 'exactly one unexpired'
}

function New-Submission {
    @{
        id = 'draft-1'; status = 'PendingCommit'; pricing = @{ priceId = 'Free' }; targetPublishMode = 'Manual'
        applicationPackages = @(@{ fileName = 'app.msixupload'; version = '3.2.1.0' })
        listings = @{
            'en-us' = @{
                baseListing = @{ title = 'Timer'; description = 'Existing description'; releaseNotes = 'Old' }
                platformOverrides = @{ Windows81 = @{ releaseNotes = 'Old override'; title = 'Platform title' } }
            }
            'en-gb' = @{ baseListing = @{ title = 'Timer UK'; releaseNotes = 'Old UK' } }
            'fr-fr' = @{ baseListing = @{ title = 'Minuteur'; releaseNotes = 'Traduction' } }
        }
    }
}
Test-Case 'release notes update only English and preserve all other submission metadata' {
    $submission = New-Submission
    $notes = "  Fixed ""pause"" & stop.`nMore reliable shutdown.  "
    Set-StoreSubmissionReleaseNotes -Submission $submission -ReleaseNotes $notes
    Assert ($submission.listings['en-us'].baseListing.releaseNotes -ceq $notes.Trim()) 'English notes lost quotes/newlines.'
    Assert ($submission.listings['en-gb'].baseListing.releaseNotes -ceq $notes.Trim()) 'UK notes not updated.'
    Assert ($submission.listings['en-us'].platformOverrides.Windows81.releaseNotes -ceq $notes.Trim()) 'Existing English override stale.'
    Assert ($submission.listings['fr-fr'].baseListing.releaseNotes -ceq 'Traduction') 'French notes overwritten.'
    Assert ($submission.pricing.priceId -eq 'Free' -and $submission.targetPublishMode -eq 'Manual') 'Pricing/release settings changed.'
    Assert ($submission.applicationPackages[0].version -eq '3.2.1.0') 'Package changed.'
    Assert ($submission.listings['en-us'].baseListing.description -eq 'Existing description') 'Description replaced.'
}
Test-Case 'no English listing fails rather than inventing an incomplete listing' {
    Set-StoreSubmissionReleaseNotes -Submission @{ listings = @{ 'fr-fr' = @{ baseListing = @{} } } } -ReleaseNotes 'Fixes.'
} 'No existing English'
Test-Case 'invalid English listing fails before partially updating any listing' {
    $submission = New-Submission
    $submission.listings['en-gb'].baseListing = $null
    try { Set-StoreSubmissionReleaseNotes -Submission $submission -ReleaseNotes 'Fixes.' } finally {
        Assert ($submission.listings['en-us'].baseListing.releaseNotes -eq 'Old') 'Partially changed English notes.'
    }
} 'has no base listing'

# Shadow both external entry points so these tests can never publish or contact either service.
function msstore {
    $global:StoreReleaseTestCalls.Add(@($args))
    $global:LASTEXITCODE = $global:StoreReleaseTestCliExitCode
}
function Invoke-RestMethod {
    param($Method, $Uri, $Headers, $TimeoutSec, $ContentType, $Body)
    $global:StoreReleaseTestRequests.Add("$Method $Uri")
    if ($TimeoutSec -ne 60) { throw 'Unbounded HTTP request.' }
    if ($global:StoreReleaseTestHttpFailure) { throw 'Simulated HTTP failure.' }
    if ($Uri -like '*/oauth2/token') {
        return @{ access_token = $(if ($global:StoreReleaseTestScenario -eq 'missing-token') { '' } else { 'local-test-token' }) }
    }
    if ($Headers.Authorization -ne 'Bearer local-test-token') { throw 'Missing authentication.' }
    if ($Uri -like '*/applications/localproduct') {
        $pending = if ($global:StoreReleaseTestCalls.Count -gt 0 -or $global:StoreReleaseTestExistingDraft) { @{ id = 'draft-1' } } else { $null }
        if ($global:StoreReleaseTestScenario -eq 'missing-draft') { $pending = $null }
        return @{ pendingApplicationSubmission = $pending }
    }
    if ($Method -eq 'Put') {
        if ($global:StoreReleaseTestScenario -eq 'metadata-failure') { throw 'Simulated metadata update failure.' }
        $global:StoreReleaseTestSaved = [Text.Encoding]::UTF8.GetString($Body) | ConvertFrom-Json -AsHashtable
        if ($global:StoreReleaseTestScenario -eq 'notes-not-saved') {
            $global:StoreReleaseTestSaved.listings['en-us'].baseListing.releaseNotes = 'Old'
        }
        if ($global:StoreReleaseTestScenario -eq 'override-not-saved') {
            $global:StoreReleaseTestSaved.listings['en-us'].platformOverrides.Windows81.releaseNotes = 'Old override'
        }
        return
    }
    if ($Method -eq 'Get') {
        if ($global:StoreReleaseTestSaved) { return $global:StoreReleaseTestSaved | ConvertTo-Json -Depth 100 | ConvertFrom-Json }
        $submission = New-Submission
        if ($global:StoreReleaseTestScenario -eq 'missing-English') {
            $submission.listings.Remove('en-us')
            $submission.listings.Remove('en-gb')
        }
        if ($global:StoreReleaseTestScenario -eq 'draft-not-ready') { $submission.status = 'Certification' }
        return $submission | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    }
    if ($Method -eq 'Post' -and $Uri -like '*/draft-1/commit') {
        return [pscustomobject]@{ status = $(if ($global:StoreReleaseTestScenario -eq 'commit-rejected') { 'CommitFailed' } else { 'CommitStarted' }) }
    }
    throw "Unexpected test request: $Method $Uri"
}

try {
    foreach ($scenario in @(
        'flight', 'missing-flight', 'production', 'missing-notes', 'existing-draft', 'http-failure', 'upload-failure',
        'missing-token', 'missing-draft', 'missing-English', 'draft-not-ready', 'metadata-failure', 'notes-not-saved',
        'override-not-saved', 'commit-rejected'
    )) {
        Test-Case "publish sequence: $scenario" {
            $global:StoreReleaseTestScenario = $scenario
            $global:StoreReleaseTestCalls = [Collections.Generic.List[object]]::new()
            $global:StoreReleaseTestRequests = [Collections.Generic.List[string]]::new()
            $global:StoreReleaseTestSaved = $null
            $global:StoreReleaseTestExistingDraft = $scenario -eq 'existing-draft'
            $global:StoreReleaseTestHttpFailure = $scenario -eq 'http-failure'
            $global:StoreReleaseTestCliExitCode = if ($scenario -eq 'upload-failure') { 1 } else { 0 }
            $parameters = @{ Destination = 'production'; ProductId = 'localproduct'; PackagePath = 'C:\test\app.msixupload'; ReleaseNotes = 'New fixes.' }
            if ($scenario -in @('flight', 'missing-flight')) {
                $parameters.Destination = 'flight'
                if ($scenario -eq 'flight') { $parameters.FlightId = 'flight-a' }
            }
            if ($scenario -eq 'missing-notes') { $parameters.ReleaseNotes = ' ' }
            $failure = $null
            try { & (Join-Path $PSScriptRoot 'Publish-StorePackage.ps1') @parameters } catch { $failure = $_.Exception.Message }
            $commits = @($global:StoreReleaseTestRequests | Where-Object { $_ -like 'Post */commit' })
            if ($scenario -in @('flight', 'production')) {
                Assert (-not $failure) "Unexpected failure: $failure"
                $arguments = $global:StoreReleaseTestCalls[0]
                Assert ($arguments[1] -eq 'C:\test\app.msixupload' -and $arguments[-1] -ne '0') 'Not uploading the complete package.'
                Assert ($arguments -contains '--uploadTimeout' -and $arguments -contains '900') 'Explicit upload timeout lost.'
                if ($scenario -eq 'flight') {
                    Assert ($arguments -contains '--flightId' -and $arguments -notcontains '--noCommit') 'Incorrect flight route.'
                    Assert ($global:StoreReleaseTestRequests.Count -eq 0) 'Flight touched production metadata.'
                } else {
                    Assert ($arguments -contains '--noCommit' -and $arguments -notcontains '--flightId') 'Production committed before setting notes.'
                    Assert ($global:StoreReleaseTestSaved.listings['en-us'].baseListing.releaseNotes -eq 'New fixes.') 'Notes not saved.'
                    Assert ($commits.Count -eq 1) 'Production draft not committed exactly once.'
                }
            } else {
                $expectedCommitAttempts = if ($scenario -eq 'commit-rejected') { 1 } else { 0 }
                Assert ($failure -and $commits.Count -eq $expectedCommitAttempts) 'Failed publication was committed or reported success.'
                if ($scenario -in @('missing-flight', 'missing-notes', 'existing-draft', 'http-failure', 'missing-token')) {
                    Assert ($global:StoreReleaseTestCalls.Count -eq 0) 'Validation failure still uploaded a package.'
                }
            }
        }
    }
} finally {
    Remove-Variable -Name StoreReleaseTest* -Scope Global -ErrorAction SilentlyContinue
    $global:LASTEXITCODE = $previousNativeExitCode
}
Write-Host "$passed Store release cases passed. No real HTTP requests, uploads, or Store changes were made."
