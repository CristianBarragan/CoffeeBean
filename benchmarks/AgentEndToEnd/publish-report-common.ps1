[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Run1','Run2','Run3','Run4')][string]$Run,
    [string]$ReportRoot,
    [string]$DestinationRoot
)
$ErrorActionPreference = 'Stop'
$BenchmarkRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $BenchmarkRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Join-Path $RepoRoot 'docs-site\assets\agent-benchmark'
}
$source = if ([string]::IsNullOrWhiteSpace($ReportRoot)) {
    switch ($Run) {
        # NOTE: Run1's runner (run-agent-benchmark.ps1) writes tier folders
        # directly under 'Run1\artifacts\<tier>\concurrency-XXX\' - there is
        # no 'agent-benchmark' subfolder in that layout. The previous mapping
        # here ('Run1\artifacts\agent-benchmark') didn't match reality and
        # made every Run1 publish fail with a path-not-found error.
        'Run1' { Join-Path $BenchmarkRoot 'Run1\artifacts' }
        'Run2' { Join-Path $BenchmarkRoot 'Run2\artifacts' }
        'Run3' { Join-Path $BenchmarkRoot 'Run3\artifacts' }
        'Run4' { Join-Path $BenchmarkRoot 'Run4\artifacts' }
    }
} else { (Resolve-Path $ReportRoot).Path }
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Benchmark report directory not found: $source"
}
$destination = Join-Path $DestinationRoot $Run.ToLowerInvariant()
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$files = Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object {
    $_.Name -in @('agent-benchmark.json','agent-benchmark.md','docker-metrics.csv','docker-metrics-summary.json','expected-state.json','run4-metadata.json','run4-summary.json')
}
if ($files.Count -eq 0) {
    throw "No publishable benchmark artifacts found under: $source"
}
foreach ($file in $files) {
    $relative = $file.FullName.Substring($source.Length).TrimStart('\','/')
    $target = Join-Path $destination $relative
    $targetDir = Split-Path -Parent $target
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Copy-Item -Force -LiteralPath $file.FullName -Destination $target
}
$manifestPath = Join-Path $destination 'publish-manifest.json'
$manifest = [ordered]@{
    run = $Run
    publishedUtc = (Get-Date).ToUniversalTime().ToString('o')
    source = $source
    destination = $destination
    files = @($files | ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($source.Length).TrimStart('\','/')
            bytes = $_.Length
            lastWriteUtc = $_.LastWriteTimeUtc.ToString('o')
        }
    })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "Published $Run benchmark artifacts to:" -ForegroundColor Green
Write-Host "  $destination"
Write-Host "  Files: $($files.Count)"