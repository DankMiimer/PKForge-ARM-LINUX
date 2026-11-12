# Read ELF64 metadata directly so Windows packaging does not depend on WSL.
function Read-NativeElf([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 64 -or $bytes[0] -ne 127 -or [Text.Encoding]::ASCII.GetString($bytes, 1, 3) -ne 'ELF' -or $bytes[4] -ne 2 -or $bytes[5] -ne 1 -or [BitConverter]::ToUInt16($bytes, 18) -ne 183) { throw "Not an ARM64 ELF64 binary: $Path" }
    $table = [BitConverter]::ToUInt64($bytes, 40)
    $size = [BitConverter]::ToUInt16($bytes, 58)
    $count = [BitConverter]::ToUInt16($bytes, 60)
    $sections = @(for ($i = 0; $i -lt $count; $i++) {
        $at = [int]($table + $i * $size)
        [pscustomobject]@{ type = [BitConverter]::ToUInt32($bytes, $at+4); offset = [BitConverter]::ToUInt64($bytes, $at+24); size = [BitConverter]::ToUInt64($bytes, $at+32); link = [BitConverter]::ToUInt32($bytes, $at+40) }
    })
    function Read-ElfString([int]$At) {
        $end = $At
        while ($end -lt $bytes.Length -and $bytes[$end] -ne 0) { $end++ }
        [Text.Encoding]::ASCII.GetString($bytes, $At, $end - $At)
    }
    $needed = @(); $versions = @()
    foreach ($section in $sections) {
        if ($section.type -eq 6) {
            $strings = $sections[$section.link].offset
            for ($at = $section.offset; $at -lt $section.offset + $section.size; $at += 16) {
                if ([BitConverter]::ToUInt64($bytes, [int]$at) -eq 1) { $needed += Read-ElfString ([int]($strings + [BitConverter]::ToUInt64($bytes, [int]$at+8))) }
            }
        }
        if ($section.type -eq 0x6ffffffe) {
            $strings = $sections[$section.link].offset
            $at = $section.offset
            do {
                $aux = $at + [BitConverter]::ToUInt32($bytes, [int]$at+8)
                for ($j = 0; $j -lt [BitConverter]::ToUInt16($bytes, [int]$at+2); $j++) {
                    $versions += Read-ElfString ([int]($strings + [BitConverter]::ToUInt32($bytes, [int]$aux+8)))
                    $aux += [BitConverter]::ToUInt32($bytes, [int]$aux+12)
                }
                $next = [BitConverter]::ToUInt32($bytes, [int]$at+12); $at += $next
            } while ($next -ne 0)
        }
    }
    $glibc = @($versions | Where-Object { $_ -match '^GLIBC_[0-9.]+$' } | ForEach-Object { [version]$_.Substring(6) } | Sort-Object -Descending)[0]
    if ($glibc -gt [version]'2.29') { throw "Native ABI exceeds glibc 2.29: $Path requires $glibc" }
    [ordered]@{ path = [IO.Path]::GetFileName($Path); bytes = $bytes.Length; sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant(); machine = 'aarch64'; glibc = "$glibc"; needed = @($needed); symbolVersions = @($versions | Sort-Object -Unique) }
}
function Write-NativeManifest([string]$App, $Assets) {
    $files = @(Get-ChildItem -LiteralPath $App -File | Where-Object { $_.Name -eq 'pkforge' -or $_.Name -match '\.so($|\.)' } | Sort-Object Name | ForEach-Object { Read-NativeElf $_.FullName })
    $packages = @($Assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { [ordered]@{ name = $_.Name; sha512 = $_.Value.sha512 } })
    $manifest = [ordered]@{ schema = 1; architecture = 'aarch64'; minimumGlibc = '2.29'; icu = '72.1.0.3'; requiredSystem = @('SDL2', 'glibc', 'libgcc_s', 'libstdc++', 'zlib'); optionalSystem = @('SDL2_mixer', 'EGL/GLES', 'OpenSSL for HTTPS'); packages = $packages; files = $files }
    [IO.File]::WriteAllText((Join-Path $App 'native-dependencies.json'), (ConvertTo-Json -InputObject $manifest -Depth 8))
}
