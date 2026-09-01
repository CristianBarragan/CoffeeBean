[CmdletBinding()]
param(
    [switch]$SkipDotnet,
    [switch]$SkipDocker
)
$ErrorActionPreference='Stop'
$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$BenchmarkRoot=Join-Path $RepoRoot 'benchmarks\AgentEndToEnd'
$AssetRoot=Join-Path $RepoRoot 'docs-site\assets\agent-benchmark'
$required=@(
 'run1-aggregate.json','run2-aggregate.json','run3-aggregate.json',
 'run4-aggregate.json','run5-aggregate.json','run5b-aggregate.json',
 'benchmark-matrix.json','supply-chain-aggregate.json'
)
foreach($name in $required){
  $p=Join-Path $AssetRoot $name
  if(!(Test-Path $p)){ throw "Missing published artifact: $p" }
  try { $null=Get-Content $p -Raw | ConvertFrom-Json } catch { throw "Invalid JSON: $p :: $($_.Exception.Message)" }
}
$matrix=Get-Content (Join-Path $AssetRoot 'benchmark-matrix.json') -Raw | ConvertFrom-Json
foreach($run in 'run1','run2','run3','run4','run5','run5b'){
  if($null -eq $matrix.runs.$run){ throw "Matrix is missing $run" }
  if(@($matrix.runs.$run).Count -eq 0){ throw "Matrix has no records for $run" }
}
$py=Join-Path $BenchmarkRoot 'scripts\build-unified-benchmark-reports.py'
if(!(Test-Path $py)){throw "Missing unified report builder: $py"}
if(Get-Command python -ErrorAction SilentlyContinue){
  Push-Location $RepoRoot
  try { & python $py; if($LASTEXITCODE -ne 0){throw "Unified report builder failed."} } finally { Pop-Location }
} else { Write-Warning 'python not found; skipped report regeneration.' }
if(-not $SkipDotnet){
  if(Get-Command dotnet -ErrorAction SilentlyContinue){
    Push-Location $RepoRoot
    try {
      & dotnet build 'benchmarks\AgentEndToEnd\Foundgine.AgentEndToEnd.Benchmark.csproj' --no-restore
      if($LASTEXITCODE -ne 0){throw 'Benchmark project build failed.'}
    } finally { Pop-Location }
  } else { Write-Warning 'dotnet not found; skipped .NET build.' }
}
if(-not $SkipDocker){
  if(Get-Command docker -ErrorAction SilentlyContinue){
    $compose=Join-Path $RepoRoot 'benchmarks\CoffeeBeanery.Performance\docker-compose.benchmark.yml'
    & docker compose -f $compose config --quiet
    if($LASTEXITCODE -ne 0){throw 'Benchmark Docker Compose validation failed.'}
  } else { Write-Warning 'docker not found; skipped Docker validation.' }
}
Write-Host 'Benchmark harness validation PASSED.' -ForegroundColor Green
