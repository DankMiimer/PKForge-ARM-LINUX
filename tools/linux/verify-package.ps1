param([string]$Directory = (Join-Path $PSScriptRoot '..\..\dist'), [switch]$AllowCandidate,
    [string]$ExpectedCommit, [string]$ExpectedTag)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath($Directory)
$info = Get-Content -LiteralPath (Join-Path $root 'BUILDINFO.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'source.ps1')
Test-ReleaseProvenance $info -AllowCandidate:$AllowCandidate -ExpectedCommit $ExpectedCommit -ExpectedTag $ExpectedTag
$stem = "PKForge-v3.2.0-linux-$($info.linux)"
$expected = @("$stem-arm64.zip", "$stem-full-source.zip", 'BUILDINFO.json')
$expected += @('tg5040', 'tg5050', 'h700' | ForEach-Object { "$stem-nextui-$_-arm64.zip" })
$listed = @()
foreach ($line in (Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$') { throw 'Malformed checksum line.' }
    $listed += $Matches[2]
    if ((Get-FileHash -LiteralPath (Join-Path $root $Matches[2]) -Algorithm SHA256).Hash -ne $Matches[1]) { throw 'Release checksum mismatch.' }
}
if ($listed.Count -ne $expected.Count -or (Compare-Object ($listed | Sort-Object) ($expected | Sort-Object))) { throw 'Checksum list must cover exactly all four binary packages, complete source and build information.' }
function Test-BinaryPackage([string]$binary, [string]$prefix, [string[]]$launchers) {
$zip = [IO.Compression.ZipFile]::OpenRead($binary)
try {
    $names = @($zip.Entries.FullName)
    if ($names -match '(^|/)(userdata|pkforge-full-test|\.git|TestResults|bin|obj)(/|$)' -or $names -match '\.(sav|srm|apk|pdb|log|key|p12)$') { throw 'Binary contains personal, test or development data.' }
    $requiredFiles = @('app/pkforge', 'app/launch.sh', 'app/port-version.json', 'app/libSkiaSharp.so', 'app/libicuuc.so.72.1.0.3', 'app/libicui18n.so.72.1.0.3', 'app/libicudata.so.72.1.0.3', 'app/asset-manifest.json', 'app/native-dependencies.json', 'LICENSE', 'ATTRIBUTION.md', 'BUILDINFO.json') | ForEach-Object { $prefix + $_ }
    foreach ($required in @($launchers) + @($requiredFiles)) {
        if (-not $zip.GetEntry($required)) { throw "Missing binary entry: $required" }
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry($prefix + 'BUILDINFO.json').Open())
    try { $embedded = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if (($embedded | ConvertTo-Json -Depth 8 -Compress) -cne ($info | ConvertTo-Json -Depth 8 -Compress)) { throw 'Packaged build information differs from release provenance.' }
    $reader = [IO.StreamReader]::new($zip.GetEntry($prefix + 'app/port-version.json').Open())
    try { $version = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $identity = Get-ReleaseIdentity $info.linux -Candidate:$info.candidate
    if ($version.linux -ne $info.linux -or $version.portCommit -ne $info.portCommit -or $version.status -ne $identity.status) { throw 'Packaged version/status differs from release provenance.' }
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
            if ($file.path -match '^lib(SDL2|c\.so|stdc\+\+|gcc_s|pthread|rt\.so|GL|EGL|GLES|glapi|vulkan|drm|gbm)') { throw "Firmware library must not be bundled: $($file.path)" }
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
Test-BinaryPackage (Join-Path $root "PKForge-v3.2.0-linux-$($info.linux)-arm64.zip") 'pkforge/' @('PKForge.sh', 'PKForge Pokepark.sh')
foreach ($platform in @('tg5040', 'tg5050', 'h700')) {
    Test-BinaryPackage (Join-Path $root "PKForge-v3.2.0-linux-$($info.linux)-nextui-$platform-arm64.zip") "Tools/$platform/PKForge.pak/pkforge/" @("Tools/$platform/PKForge.pak/launch.sh", "Tools/$platform/PKForge Pokepark.pak/launch.sh")
}
$source = [IO.Compression.ZipFile]::OpenRead((Join-Path $root "PKForge-v3.2.0-linux-$($info.linux)-full-source.zip"))
try {
    foreach ($path in @('SOURCE-MANIFEST.json', 'global.json', 'tools/linux/build.ps1', 'external/PKHeX/PKHeX.Core/PKHeX.Core.csproj', 'external/PKHeX-Plugins/PKHeX.Core.AutoMod/PKHeX.Core.AutoMod.csproj')) {
        if (-not $source.GetEntry($path)) { throw "Complete source entry missing: $path" }
    }
    if ($source.Entries.FullName -match '(^|/)(\.git|userdata|bin|obj|artifacts)(/|$)') { throw 'Source contains private or generated files.' }
    Write-Output "Complete source verified: $($source.Entries.Count) entries, including both submodules."
} finally { $source.Dispose() }
