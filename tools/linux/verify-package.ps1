param([string]$Directory = (Join-Path $PSScriptRoot '..\..\dist'), [switch]$AllowCandidate)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath($Directory)
$info = Get-Content -LiteralPath (Join-Path $root 'BUILDINFO.json') -Raw | ConvertFrom-Json
if (($info.dirty -and -not $AllowCandidate) -or ($info.candidate -and -not $AllowCandidate) -or $info.portCommit -notmatch '^[a-f0-9]{40}$' -or $info.rid -ne 'linux-arm64') { throw 'Invalid release provenance; candidates require -AllowCandidate.' }
foreach ($line in (Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$') { throw 'Malformed checksum line.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $Matches[2]) -Algorithm SHA256).Hash -ne $Matches[1]) { throw 'Release checksum mismatch.' }
}
function Test-BinaryPackage([string]$binary, [string]$prefix, [string[]]$launchers) {
$zip = [IO.Compression.ZipFile]::OpenRead($binary)
try {
    $names = @($zip.Entries.FullName)
    if ($names -match '(^|/)(userdata|pkforge-full-test|\.git|TestResults|bin|obj)(/|$)' -or $names -match '\.(sav|srm|apk|pdb|log|key|p12)$') { throw 'Binary contains personal, test or development data.' }
    $requiredFiles = @('app/pkforge', 'app/launch.sh', 'app/libSkiaSharp.so', 'app/libicuuc.so.72.1.0.3', 'app/libicui18n.so.72.1.0.3', 'app/libicudata.so.72.1.0.3', 'app/asset-manifest.json', 'app/native-dependencies.json', 'LICENSE', 'ATTRIBUTION.md', 'BUILDINFO.json') | ForEach-Object { $prefix + $_ }
    foreach ($required in @($launchers) + @($requiredFiles)) {
        if (-not $zip.GetEntry($required)) { throw "Missing binary entry: $required" }
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry($prefix + 'app/asset-manifest.json').Open())
    try { $assets = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($asset in $assets) {
            $entry = $zip.GetEntry($prefix + 'app/assets/' + $asset.path)
            if (-not $entry -or $entry.Length -ne $asset.bytes) { throw "Asset missing/truncated: $($asset.path)" }
            $stream = $entry.Open()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() } finally { $stream.Dispose() }
            if ($hash -ne $asset.sha256) { throw "Asset checksum mismatch: $($asset.path)" }
        }
    } finally { $sha.Dispose() }
    $reader = [IO.StreamReader]::new($zip.GetEntry($prefix + 'app/native-dependencies.json').Open())
    try { $native = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($native.architecture -ne 'aarch64' -or $native.minimumGlibc -ne '2.29' -or $native.files.Count -lt 10) { throw 'Invalid native dependency manifest.' }
    $nativePaths = @($native.files | ForEach-Object { $prefix + 'app/' + $_.path })
    foreach ($entry in $zip.Entries | Where-Object { $_.FullName -match '/app/[^/]+\.so($|\.)' }) {
        if ($entry.FullName -notin $nativePaths) { throw "Unmanifested native library: $($entry.FullName)" }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($file in $native.files) {
            if ($file.path -match '[/\\]' -or $file.machine -ne 'aarch64' -or ($file.glibc -and [version]$file.glibc -gt [version]'2.29')) { throw 'Invalid native ABI.' }
            $entry = $zip.GetEntry($prefix + 'app/' + $file.path)
            if (-not $entry -or $entry.Length -ne $file.bytes) { throw 'Missing native binary.' }
            $stream = $entry.Open()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() } finally { $stream.Dispose() }
            if ($hash -ne $file.sha256) { throw "Native checksum mismatch: $($file.path)" }
        }
    } finally { $sha.Dispose() }
    $reader = [IO.StreamReader]::new($zip.GetEntry($prefix + 'app/pkforge.runtimeconfig.json').Open())
    try { $runtime = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($runtime.runtimeOptions.configProperties.'System.Globalization.AppLocalIcu' -ne '72.1.0.3') { throw 'App-local ICU is not enabled.' }
    foreach ($entry in $zip.Entries | Where-Object { $_.FullName.EndsWith('.sh') -or $_.FullName.EndsWith('/app/pkforge') }) {
        if ((($entry.ExternalAttributes -shr 16) -band 73) -ne 73) { throw 'Executable permission missing.' }
        if ($entry.FullName.EndsWith('.sh')) {
            $reader = [IO.StreamReader]::new($entry.Open())
            try { if ($reader.ReadToEnd().Contains("`r")) { throw 'Shell launcher has CRLF line endings.' } } finally { $reader.Dispose() }
        }
    }
    Write-Output "Verified $([IO.Path]::GetFileName($binary)): $($zip.Entries.Count) entries, $($assets.Count) assets, $($native.files.Count) ARM64 binaries; no personal data."
} finally { $zip.Dispose() }
}
Test-BinaryPackage (Join-Path $root "PKForge-v3.1.1-linux-$($info.linux)-arm64.zip") 'pkforge/' @('PKForge.sh', 'PKForge Pokepark.sh')
foreach ($platform in @('tg5040', 'tg5050', 'h700')) {
    Test-BinaryPackage (Join-Path $root "PKForge-v3.1.1-linux-$($info.linux)-nextui-$platform-arm64.zip") "Tools/$platform/PKForge.pak/pkforge/" @("Tools/$platform/PKForge.pak/launch.sh", "Tools/$platform/PKForge Pokepark.pak/launch.sh")
}
$source = [IO.Compression.ZipFile]::OpenRead((Join-Path $root "PKForge-v3.1.1-linux-$($info.linux)-full-source.zip"))
try {
    foreach ($path in @('SOURCE-MANIFEST.json', 'global.json', 'tools/linux/build.ps1', 'external/PKHeX/PKHeX.Core/PKHeX.Core.csproj', 'external/PKHeX-Plugins/PKHeX.Core.AutoMod/PKHeX.Core.AutoMod.csproj')) {
        if (-not $source.GetEntry($path)) { throw "Complete source entry missing: $path" }
    }
    if ($source.Entries.FullName -match '(^|/)(\.git|userdata|bin|obj|artifacts)(/|$)') { throw 'Source contains private or generated files.' }
    Write-Output "Complete source verified: $($source.Entries.Count) entries, including both submodules."
} finally { $source.Dispose() }
