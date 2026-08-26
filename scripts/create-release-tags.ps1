[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [switch]$Mac,
    [switch]$Windows,
    [switch]$Push
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Version -notmatch '^v\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version must match v<major>.<minor>.<patch> or v<major>.<minor>.<patch>.<revision> (example: v3.0.0 or v3.0.0.1)."
}

if (-not $Mac -and -not $Windows) {
    throw "Select at least one platform with -Mac and/or -Windows."
}

$selectedTags = @()
if ($Mac) {
    $selectedTags += "$Version-mac"
}
if ($Windows) {
    $selectedTags += "$Version-windows"
}

git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) {
    throw "This script must run inside a git repository."
}

function Test-LocalTagExists {
    param([Parameter(Mandatory = $true)][string]$Tag)

    git rev-parse -q --verify "refs/tags/$Tag" *> $null
    return $LASTEXITCODE -eq 0
}

function Test-RemoteTagExists {
    param([Parameter(Mandatory = $true)][string]$Tag)

    $matches = git ls-remote --tags origin "refs/tags/$Tag" 2>$null
    return -not [string]::IsNullOrWhiteSpace($matches)
}

foreach ($tag in $selectedTags) {
    if (Test-LocalTagExists -Tag $tag) {
        throw "Tag '$tag' already exists locally."
    }
    if (Test-RemoteTagExists -Tag $tag) {
        throw "Tag '$tag' already exists on origin."
    }
}

foreach ($tag in $selectedTags) {
    $label = if ($tag -like "*-windows") { "Windows" } elseif ($tag -like "*-mac") { "macOS" } else { "Release" }
    if ($PSCmdlet.ShouldProcess($tag, "Create annotated $label tag")) {
        git tag -a $tag -m "Release $tag"
    }
}

if ($Push) {
    $tagList = $selectedTags -join " "
    if ($PSCmdlet.ShouldProcess("origin", "Push tags $tagList")) {
        git push origin @selectedTags
    }
}

Write-Host "Created tags:"
foreach ($tag in $selectedTags) {
    Write-Host "- $tag"
}
if (-not $Push) {
    Write-Host ("Push with: git push origin {0}" -f ($selectedTags -join " "))
}
