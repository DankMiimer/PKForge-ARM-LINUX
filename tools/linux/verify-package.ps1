param([string]$Directory = (Join-Path $PSScriptRoot '..\..\dist'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath($Directory)
$info = Get-Content -LiteralPath (Join-Path $root 'BUILDINFO.json') -Raw | ConvertFrom-Json
if ($info.dirty -ne $false -or $info.portCommit -notmatch '^[a-f0-9]{40}$' -or $info.rid -ne 'linux-arm64') { throw 'Invalid release provenance.' }
foreach ($line in (Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([a-f0-9]{64})  ([^/\\]+)$') { throw 'Malformed checksum line.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $Matches[2]) -Algorithm SHA256).Hash -ne $Matches[1]) { throw 'Release checksum mismatch.' }
}
$binary = Join-Path $root "PKForge-v3.1.1-linux-$($info.linux)-arm64.zip"
$zip = [IO.Compression.ZipFile]::OpenRead($binary)
try {
    $names = @($zip.Entries.FullName)
    if ($names -match '(^|/)(userdata|pkforge-full-test|\.git|TestResults|bin|obj)(/|$)' -or $names -match '\.(sav|srm|apk|pdb|log|key|p12)$') { throw 'Binary contains personal, test or development data.' }
    foreach ($required in @('PKForge.sh', 'PKForge Pokepark.sh', 'pkforge/app/pkforge', 'pkforge/app/libSkiaSharp.so', 'pkforge/app/asset-manifest.json', 'pkforge/LICENSE', 'pkforge/ATTRIBUTION.md', 'pkforge/BUILDINFO.json')) {
        if (-not $zip.GetEntry($required)) { throw "Missing binary entry: $required" }
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry('pkforge/app/asset-manifest.json').Open())
    try { $assets = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($asset in $assets) {
            $entry = $zip.GetEntry('pkforge/app/assets/' + $asset.path)
            if (-not $entry -or $entry.Length -ne $asset.bytes) { throw "Asset missing/truncated: $($asset.path)" }
            $stream = $entry.Open()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() } finally { $stream.Dispose() }
            if ($hash -ne $asset.sha256) { throw "Asset checksum mismatch: $($asset.path)" }
        }
    } finally { $sha.Dispose() }
    foreach ($entry in $zip.Entries | Where-Object { $_.FullName.EndsWith('.sh') -or $_.FullName -eq 'pkforge/app/pkforge' }) {
        if ((($entry.ExternalAttributes -shr 16) -band 73) -ne 73) { throw 'Executable permission missing.' }
        if ($entry.FullName.EndsWith('.sh')) {
            $reader = [IO.StreamReader]::new($entry.Open())
            try { if ($reader.ReadToEnd().Contains("`r")) { throw 'Shell launcher has CRLF line endings.' } } finally { $reader.Dispose() }
        }
    }
    Write-Output "Binary verified: $($zip.Entries.Count) entries, $($assets.Count) assets; no personal data."
} finally { $zip.Dispose() }
$source = [IO.Compression.ZipFile]::OpenRead((Join-Path $root "PKForge-v3.1.1-linux-$($info.linux)-full-source.zip"))
try {
    foreach ($path in @('SOURCE-MANIFEST.json', 'global.json', 'tools/linux/build.ps1', 'external/PKHeX/PKHeX.Core/PKHeX.Core.csproj', 'external/PKHeX-Plugins/PKHeX.Core.AutoMod/PKHeX.Core.AutoMod.csproj')) {
        if (-not $source.GetEntry($path)) { throw "Complete source entry missing: $path" }
    }
    if ($source.Entries.FullName -match '(^|/)(\.git|userdata|bin|obj|artifacts)(/|$)') { throw 'Source contains private or generated files.' }
    Write-Output "Complete source verified: $($source.Entries.Count) entries, including both submodules."
} finally { $source.Dispose() }
