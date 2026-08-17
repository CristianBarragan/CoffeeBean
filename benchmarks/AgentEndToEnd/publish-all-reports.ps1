[CmdletBinding()]
param([string]$Destination)
$ErrorActionPreference='Stop'
foreach($run in @('Run1','Run2','Run3','Run4')) {
    $script=Join-Path $PSScriptRoot "$run\publish-report.ps1"
    try {
        & $script -Destination $Destination
        if (-not $?) { throw "Publish script returned failure for $run." }
    } catch {
        throw "Failed to publish $run. $($_.Exception.Message)"
    }
}
& (Join-Path $PSScriptRoot 'build-benchmark-matrix.ps1') -Destination (Join-Path ((Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path) 'docs-site\assets\agent-benchmark\benchmark-matrix.json')
if (-not $?) { throw 'Failed to build benchmark matrix.' }
Write-Host 'All benchmark reports and the interactive benchmark matrix are published.' -ForegroundColor Green
