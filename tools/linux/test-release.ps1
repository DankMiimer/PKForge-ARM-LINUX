$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'source.ps1')
function Assert-Equal($Expected, $Actual) {
    if ($Expected -cne $Actual) { throw "Expected '$Expected', got '$Actual'." }
}
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'Invalid release provenance was accepted.' }
}
$commit = 'a' * 40
$stable = Get-ReleaseIdentity '0.3.0'
Assert-Equal 'stable' $stable.channel
Assert-Equal 'stable' $stable.status
Assert-Equal $false $stable.prerelease
Assert-Equal 'linux-v0.3.0' $stable.tag
Assert-Equal 'PKForge 3.2.0 — Linux port 0.3.0' $stable.title
Assert-Equal 'preview' (Get-ReleaseIdentity '0.3.0-rc.1').channel
Assert-Equal 'prerelease' (Get-ReleaseIdentity '0.2.4-dev').status
Assert-Equal 'candidate' (Get-ReleaseIdentity '0.3.0' -Candidate).channel
foreach ($revision in @('', 'v0.3.0', '0.3', '0.03.0', '../0.3.0', "0.3.0`n")) {
    Assert-Rejected { Get-ReleaseIdentity $revision }
}
$info = [pscustomobject]@{ portCommit=$commit; rid='linux-arm64'; linux='0.3.0'; channel='stable'; dirty=$false; candidate=$false }
Test-ReleaseProvenance $info -ExpectedCommit $commit -ExpectedTag 'linux-v0.3.0'
Assert-Rejected { Test-ReleaseProvenance $info -ExpectedCommit ('b' * 40) }
Assert-Rejected { Test-ReleaseProvenance $info -ExpectedTag 'linux-v0.3.1' }
$info.dirty = $true
Assert-Rejected { Test-ReleaseProvenance $info -AllowCandidate }
$info.candidate = $true; $info.channel = 'candidate'
Assert-Rejected { Test-ReleaseProvenance $info }
Test-ReleaseProvenance $info -AllowCandidate
$info.channel = 'stable'
Assert-Rejected { Test-ReleaseProvenance $info -AllowCandidate }
Write-Output 'Release channel, candidate isolation, tag and commit guards passed.'
