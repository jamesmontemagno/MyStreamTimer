param(
    [Parameter(Mandatory)]
    [string] $PackagePath,
    [string] $SymbolsDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression

function Open-EntryStream($Entry) {
    $memory = [System.IO.MemoryStream]::new()
    $source = $Entry.Open()
    try { $source.CopyTo($memory) } finally { $source.Dispose() }
    $memory.Position = 0
    return $memory
}

function Open-EntryArchive($Entry) {
    $stream = Open-EntryStream $Entry
    return [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
}

function Read-EntryText($Archive, [string] $Name) {
    $entry = $Archive.GetEntry($Name)
    if (-not $entry) { throw "Missing package entry: $Name" }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Test-MatchingSymbol($AssemblyEntry, $SymbolEntry) {
    $assemblyStream = Open-EntryStream $AssemblyEntry
    $symbolStream = Open-EntryStream $SymbolEntry
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($assemblyStream)
    $pdb = $null
    try {
        $pdb = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($symbolStream)
        $id = [byte[]] $pdb.GetMetadataReader().DebugMetadataHeader.Id
        $guid = [guid]::new([byte[]] ($id[0..15]))
        $stamp = [BitConverter]::ToUInt32($id, 16)
        foreach ($debugEntry in $pe.ReadDebugDirectory()) {
            if ($debugEntry.Type -eq [System.Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView) {
                $codeView = $pe.ReadCodeViewDebugDirectoryData($debugEntry)
                if ($codeView.Guid -eq $guid -and $codeView.Age -eq 1 -and $debugEntry.Stamp -eq $stamp) {
                    return $true
                }
            }
        }
        return $false
    } finally {
        if ($pdb) { $pdb.Dispose() }
        $pe.Dispose()
        $assemblyStream.Dispose()
        $symbolStream.Dispose()
    }
}

$upload = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
$symbolArchives = [System.Collections.Generic.List[System.IO.Compression.ZipArchive]]::new()
$bundle = $null
try {
    $bundleEntries = @($upload.Entries | Where-Object FullName -Match '\.(msixbundle|appxbundle)$')
    if ($bundleEntries.Count -ne 1) { throw 'Expected exactly one Store bundle.' }
    $bundle = Open-EntryArchive $bundleEntries[0]
    foreach ($entry in $upload.Entries | Where-Object FullName -Match '\.(appxsym|msixsym)$') {
        $symbolArchives.Add((Open-EntryArchive $entry))
    }
    if ($symbolArchives.Count -eq 0) { throw 'The Store upload has no symbol package.' }

    foreach ($architecture in @('x64', 'arm64')) {
        $entries = @($bundle.Entries | Where-Object FullName -Match "_$architecture\.(msix|appx)$")
        if ($entries.Count -ne 1) { throw "Expected exactly one $architecture application package." }
        $payload = Open-EntryArchive $entries[0]
        try {
            foreach ($required in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll', 'MyStreamTimer.exe')) {
                if (-not $payload.GetEntry($required)) { throw "$architecture is missing $required (not .NET self-contained)." }
            }
            $expectedMachine = if ($architecture -eq 'x64') {
                [System.Reflection.PortableExecutable.Machine]::Amd64
            } else {
                [System.Reflection.PortableExecutable.Machine]::Arm64
            }
            foreach ($nativeFile in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'MyStreamTimer.exe')) {
                $nativeStream = Open-EntryStream $payload.GetEntry($nativeFile)
                $nativePe = [System.Reflection.PortableExecutable.PEReader]::new($nativeStream)
                try {
                    if ($nativePe.PEHeaders.CoffHeader.Machine -ne $expectedMachine) {
                        throw "$architecture contains a wrong-architecture $nativeFile."
                    }
                } finally {
                    $nativePe.Dispose()
                    $nativeStream.Dispose()
                }
            }
            $config = Read-EntryText $payload 'MyStreamTimer.runtimeconfig.json' | ConvertFrom-Json -AsHashtable
            if ($config.runtimeOptions.ContainsKey('framework') -or $config.runtimeOptions.ContainsKey('frameworks')) {
                throw "$architecture still requires a separately installed .NET framework."
            }
            if (-not ($config.runtimeOptions.includedFrameworks | Where-Object name -EQ 'Microsoft.NETCore.App')) {
                throw "$architecture has no bundled .NET framework metadata."
            }
            $manifest = [xml] (Read-EntryText $payload 'AppxManifest.xml')
            if ($manifest.Package.Identity.ProcessorArchitecture -ne $architecture) {
                throw "Incorrect processor architecture for $architecture."
            }
            if (-not ($manifest.Package.Dependencies.PackageDependency | Where-Object Name -Match '^Microsoft.WindowsAppRuntime\.')) {
                throw "$architecture is missing the Store-managed Windows App Runtime dependency."
            }

            $destination = $null
            if ($SymbolsDirectory) {
                $destination = Join-Path $SymbolsDirectory $architecture
                New-Item -ItemType Directory -Force -Path $destination | Out-Null
            }
            foreach ($name in @('MyStreamTimer', 'MyStreamTimer.Core')) {
                $assembly = $payload.GetEntry("$name.dll")
                if (-not $assembly) { throw "Missing $name.dll in $architecture." }
                $matched = $null
                :symbols foreach ($archive in $symbolArchives) {
                    foreach ($symbol in $archive.Entries | Where-Object { [System.IO.Path]::GetFileName($_.FullName) -eq "$name.pdb" }) {
                        if (Test-MatchingSymbol $assembly $symbol) {
                            $matched = $symbol
                            break symbols
                        }
                    }
                }
                if (-not $matched) { throw "No matching portable PDB for $architecture $name.dll." }
                if ($destination) {
                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($assembly, (Join-Path $destination "$name.dll"), $true)
                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($matched, (Join-Path $destination "$name.pdb"), $true)
                }
            }
            if ($destination) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
                    $payload.GetEntry('MyStreamTimer.exe'), (Join-Path $destination 'MyStreamTimer.exe'), $true)
            }
            Write-Host "${architecture}: bundled .NET runtime and matching application/Core PDBs verified."
        } finally { $payload.Dispose() }
    }
    if ($SymbolsDirectory) {
        @{
            commit = $env:GITHUB_SHA
            run = $env:GITHUB_RUN_ID
            upload = [System.IO.Path]::GetFileName($PackagePath)
            sha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $SymbolsDirectory 'build-provenance.json') -Encoding utf8
    }
} finally {
    foreach ($archive in $symbolArchives) { $archive.Dispose() }
    if ($bundle) { $bundle.Dispose() }
    $upload.Dispose()
}
