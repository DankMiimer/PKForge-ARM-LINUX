# Source export/verification shared by Git checkouts and complete source archives.
$script:UpstreamCommit = '799dfc310c94bb978962dcd31ddf7d39e6d8cba3'
$script:PinnedModules = [ordered]@{
    'external/PKHeX' = '91e9c88667a6a191a000db68e2db19505eb87561'
    'external/PKHeX-Plugins' = '23ab1fc73f7be6ad427fbc900f9b99111cd2d560'
}
function Read-SourceProvenance([string]$Root) {
    if (Test-Path -LiteralPath (Join-Path $Root '.git')) {
        $status = git -C $Root status --porcelain
        if ($LASTEXITCODE -or $status) { throw 'Build from a clean, committed checkout; generated artifacts must be ignored.' }
        git -C $Root merge-base --is-ancestor $script:UpstreamCommit HEAD
        if ($LASTEXITCODE) { throw 'The pinned PKForge v3.1.1 base is required.' }
        foreach ($module in $script:PinnedModules.Keys) {
            $moduleRoot = Join-Path $Root $module
            $actual = git -C $moduleRoot rev-parse HEAD
            if ($LASTEXITCODE -or $actual -ne $script:PinnedModules[$module]) { throw "Initialize pinned submodule: $module" }
            $moduleStatus = git -C $moduleRoot status --porcelain
            if ($LASTEXITCODE -or $moduleStatus) { throw "Submodule has local changes: $module" }
        }
        $commit = git -C $Root rev-parse HEAD
        if ($LASTEXITCODE) { throw 'Could not read the port commit.' }
        return [pscustomobject][ordered]@{
            upstream = '3.1.1'; upstreamCommit = $script:UpstreamCommit; portCommit = $commit
            submodules = [pscustomobject]$script:PinnedModules; dirty = $false
        }
    }
    $manifestPath = Join-Path $Root 'SOURCE-MANIFEST.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Use a recursive Git clone or the complete source release archive.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or -not $manifest.files -or $manifest.provenance.upstreamCommit -ne $script:UpstreamCommit) { throw 'Unsupported source manifest.' }
    foreach ($module in $script:PinnedModules.Keys) {
        if ($manifest.provenance.submodules.$module -ne $script:PinnedModules[$module]) { throw "Source submodule mismatch: $module" }
    }
    foreach ($file in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $Root $file.path))
        if (-not $path.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Source file missing or changed: $($file.path)"
        }
    }
    Write-Host "Verified $($manifest.files.Count) source files, including pinned submodules."
    return $manifest.provenance
}
function Export-CompleteSource([string]$Root, [string]$Destination, $Provenance) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    if (Test-Path -LiteralPath (Join-Path $Root '.git')) {
        $trees = @([pscustomobject]@{ root = $Root; prefix = '' })
        $trees += @($script:PinnedModules.Keys | ForEach-Object { [pscustomobject]@{ root = (Join-Path $Root $_); prefix = $_ } })
        foreach ($tree in $trees) {
            $temporaryZip = Join-Path (Split-Path $Destination -Parent) ('source-tree-' + [guid]::NewGuid().ToString('N') + '.zip')
            git -C $tree.root archive HEAD --format=zip -o $temporaryZip
            if ($LASTEXITCODE) { throw 'Git source export failed.' }
            $outputRoot = if ($tree.prefix) { Join-Path $Destination $tree.prefix } else { $Destination }
            New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
            try { [IO.Compression.ZipFile]::ExtractToDirectory($temporaryZip, $outputRoot) }
            finally { Remove-Item -LiteralPath $temporaryZip -Force }
        }
    }
    else {
        $manifest = Get-Content -LiteralPath (Join-Path $Root 'SOURCE-MANIFEST.json') -Raw | ConvertFrom-Json
        foreach ($file in $manifest.files) {
            $target = Join-Path $Destination $file.path
            New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
            Copy-Item -LiteralPath (Join-Path $Root $file.path) -Destination $target
        }
    }
    $files = @(Get-ChildItem -LiteralPath $Destination -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($Destination.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    [IO.File]::WriteAllText((Join-Path $Destination 'SOURCE-MANIFEST.json'), (ConvertTo-Json -Depth 6 -InputObject ([ordered]@{ schema = 1; provenance = $Provenance; files = $files })))
}
