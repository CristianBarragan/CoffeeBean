[CmdletBinding()]
param([string]$Destination)
$ErrorActionPreference='Stop'
$script=Join-Path $PSScriptRoot 'scripts\build-unified-benchmark-reports.py'
if(-not(Test-Path -LiteralPath $script -PathType Leaf)){throw "Unified benchmark report builder not found: $script"}
& python $script
if(-not $?){throw 'Unified benchmark report builder failed.'}
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$source=Join-Path $repoRoot 'docs-site\assets\agent-benchmark\benchmark-matrix.json'
if([string]::IsNullOrWhiteSpace($Destination)){$Destination=$source}
if((Resolve-Path $source).Path -ne $Destination){
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
  Copy-Item -Force -LiteralPath $source -Destination $Destination
}
Write-Host "Built canonical benchmark matrix: $Destination" -ForegroundColor Green
