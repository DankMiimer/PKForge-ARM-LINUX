param(
    [Parameter(Mandatory)][string]$Serial,
    [Parameter(Mandatory)][string]$Name,
    [string]$OutputDirectory = "artifacts/ui-parity/android-reference",
    [ValidateRange(0, 30)][int]$VideoSeconds = 0
)
$ErrorActionPreference = "Stop"
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw "Use a simple capture name." }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new("adb")
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.CreateNoWindow = $true
foreach ($argument in @("-s", $Serial, "exec-out")) { $start.ArgumentList.Add($argument) }
if ($VideoSeconds -gt 0) {
    foreach ($argument in @("screenrecord", "--time-limit", "$VideoSeconds", "--bit-rate", "8000000", "--output-format=h264", "-")) { $start.ArgumentList.Add($argument) }
    $extension = "h264"
} else {
    foreach ($argument in @("screencap", "-p")) { $start.ArgumentList.Add($argument) }
    $extension = "png"
}
$path = Join-Path $destination "$Name.$extension"
$process = [Diagnostics.Process]::Start($start)
try {
    $stream = [IO.File]::Create($path)
    try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "adb capture failed: $($process.ExitCode)" }
} finally { $process.Dispose() }
[ordered]@{
    capturedAtUtc = [DateTime]::UtcNow.ToString("O")
    application = "org.pkforge.app"
    version = (& adb -s $Serial shell dumpsys package org.pkforge.app | Select-String 'versionName=|versionCode=' | ForEach-Object { $_.Line.Trim() })
    size = (& adb -s $Serial shell wm size)
    density = (& adb -s $Serial shell wm density)
    videoSeconds = $VideoSeconds
    note = "Read-only capture; no display overrides, save changes, or app installation. May contain personal data."
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination "$Name.json") -Encoding utf8
Write-Output $path
