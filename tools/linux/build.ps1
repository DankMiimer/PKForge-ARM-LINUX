param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$publishDirectory = Join-Path $repoRoot 'artifacts\release-publish'
$packageDirectory = Join-Path $repoRoot 'artifacts\package'
$appDirectory = Join-Path $packageDirectory 'pkforge\app'
$distributionDirectory = Join-Path $repoRoot 'dist'
$portRevision = [regex]::Match([IO.File]::ReadAllText((Join-Path $repoRoot 'src\PKForge.Linux\Program.cs')), 'public const string PortRevision = "([^"]+)";').Groups[1].Value
if (-not $portRevision) { throw 'Could not read the Linux revision.' }
function New-PortableArchive([string]$Root, [string]$Destination) {
    $zip = [IO.Compression.ZipFile]::Open($Destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in (Get-ChildItem -LiteralPath $Root -File -Recurse -Force | Sort-Object FullName)) {
            $relative = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
            $entry = $zip.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
            $mode = if ($relative.EndsWith('.sh') -or $relative -eq 'pkforge/app/pkforge') { 33261 } else { 33188 }
            $entry.ExternalAttributes = $mode -shl 16
            $inputFile = $file.OpenRead()
            $outputFile = $entry.Open()
            try { $inputFile.CopyTo($outputFile) }
            finally { $outputFile.Dispose(); $inputFile.Dispose() }
        }
    }
    finally { $zip.Dispose() }
    # Windows PowerShell's ZipArchive reports DOS as the creator. Mark the central
    # directory as Unix so Linux extractors honor the executable modes above.
    $stream = [IO.File]::Open($Destination, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        $stream.Position = $stream.Length - 22
        $end = $reader.ReadBytes(22)
        if ([BitConverter]::ToUInt32($end, 0) -ne 0x06054b50 -or [BitConverter]::ToUInt16($end, 10) -eq 65535) { throw 'Unsupported ZIP directory.' }
        $stream.Position = [BitConverter]::ToUInt32($end, 16)
        for ($i = 0; $i -lt [BitConverter]::ToUInt16($end, 10); $i++) {
            $position = $stream.Position
            $header = $reader.ReadBytes(46)
            if ([BitConverter]::ToUInt32($header, 0) -ne 0x02014b50) { throw 'Invalid ZIP directory.' }
            $stream.Position = $position + 5
            $stream.WriteByte(3)
            $stream.Position = $position + 46 + [BitConverter]::ToUInt16($header, 28) + [BitConverter]::ToUInt16($header, 30) + [BitConverter]::ToUInt16($header, 32)
        }
    }
    finally { $reader.Dispose(); $stream.Dispose() }
}
Push-Location $repoRoot
try {
    . (Join-Path $PSScriptRoot 'source.ps1')
    $provenance = Read-SourceProvenance $repoRoot
    dotnet publish src/PKForge.Linux/PKForge.Linux.csproj -c $Configuration -r linux-arm64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false -o $publishDirectory --nologo
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    # Only generated directories below this repository's artifacts folder may be replaced.
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    $resolvedPackage = [IO.Path]::GetFullPath($packageDirectory)
    if (-not $resolvedPackage.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package path.' }
    if (Test-Path -LiteralPath $resolvedPackage) { Remove-Item -LiteralPath $resolvedPackage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $appDirectory,$distributionDirectory | Out-Null
    # Copy into app, preserving content paths.
    Get-ChildItem -LiteralPath $publishDirectory -Force | Where-Object Name -NotIn @('userdata', 'cache', 'exports', 'imports') | Copy-Item -Destination $appDirectory -Recurse -Force
    Get-ChildItem -LiteralPath $appDirectory -Filter '*.pdb' | Remove-Item -Force
    Copy-Item -LiteralPath 'packaging\linux\PKForge.sh','packaging\linux\PKForge Pokepark.sh' -Destination $packageDirectory
    Copy-Item -LiteralPath 'packaging\linux\port.json' -Destination (Join-Path $packageDirectory 'pkforge')
    Copy-Item -LiteralPath 'LINUX.md','VALIDATION.md','FEATURE_COVERAGE.md','ACTION_INVENTORY.md','packaging\linux\ATTRIBUTION.md','LICENSE' -Destination (Join-Path $packageDirectory 'pkforge')
    $assets = Get-Content 'src\PKForge.Linux\obj\project.assets.json' -Raw | ConvertFrom-Json
    $packageCache = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $runtimeVersion = (Get-Content (Join-Path $publishDirectory 'pkforge.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version
    $noticePackages = @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Value.path })
    $noticePackages += "microsoft.netcore.app.runtime.linux-arm64/$runtimeVersion"
    foreach ($package in $noticePackages) {
        $cachePath = Join-Path $packageCache $package
        foreach ($notice in (Get-ChildItem -LiteralPath $cachePath -File | Where-Object Name -Match '^(LICENSE|NOTICE|THIRD)')) {
            $noticeDirectory = Join-Path $appDirectory ("assets\notices\" + $package)
            New-Item -ItemType Directory -Force -Path $noticeDirectory | Out-Null
            Copy-Item -LiteralPath $notice.FullName -Destination $noticeDirectory
        }
    }
    $manifestRoot = Join-Path $appDirectory 'assets'
    $assetManifest = @(Get-ChildItem -LiteralPath $manifestRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path=$_.FullName.Substring($manifestRoot.Length+1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    [IO.File]::WriteAllText((Join-Path $appDirectory 'asset-manifest.json'), (ConvertTo-Json -InputObject $assetManifest -Depth 4))
    $buildInfo = [ordered]@{ upstream='3.1.1'; upstreamCommit=$provenance.upstreamCommit; linux=$portRevision; portCommit=$provenance.portCommit; submodules=$provenance.submodules; dirty=$false; sdk=(dotnet --version); runtime=$runtimeVersion; rid='linux-arm64'; configuration=$Configuration }
    $buildJson = ConvertTo-Json -Depth 5 -InputObject $buildInfo
    [IO.File]::WriteAllText((Join-Path $distributionDirectory 'BUILDINFO.json'), $buildJson)
    [IO.File]::WriteAllText((Join-Path $packageDirectory 'pkforge\BUILDINFO.json'), $buildJson)
    [IO.File]::WriteAllText((Join-Path $appDirectory 'port-version.json'), (ConvertTo-Json -Compress -InputObject ([ordered]@{ upstream='3.1.1'; commit=$provenance.upstreamCommit; portCommit=$provenance.portCommit; linux=$portRevision; status='prerelease' })))
    $archive = Join-Path $distributionDirectory "PKForge-v3.1.1-linux-$portRevision-arm64.zip"
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
    New-PortableArchive $packageDirectory $archive
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($archive + '.sha256', $hash + '  ' + [IO.Path]::GetFileName($archive) + "`n")
    Write-Output $archive
    # Export complete committed sources, including both pinned submodules. The
    # receipt also lets the extracted source build without a .git directory.
    $sourceDirectory = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\complete-source'))
    if (-not $sourceDirectory.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe source path.' }
    if (Test-Path -LiteralPath $sourceDirectory) { Remove-Item -LiteralPath $sourceDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $sourceDirectory | Out-Null
    Export-CompleteSource $repoRoot $sourceDirectory $provenance
    $sourceArchive = Join-Path $distributionDirectory "PKForge-v3.1.1-linux-$portRevision-full-source.zip"
    if (Test-Path -LiteralPath $sourceArchive) { Remove-Item -LiteralPath $sourceArchive }
    New-PortableArchive $sourceDirectory $sourceArchive
    Write-Output $sourceArchive
    $checksums = @($archive, $sourceArchive, (Join-Path $distributionDirectory 'BUILDINFO.json')) | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
    }
    [IO.File]::WriteAllText((Join-Path $distributionDirectory 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n")
}
finally { Pop-Location }
