$ErrorActionPreference = "Stop"

$PipelineRoot = $PSScriptRoot

Write-Host ""
Write-Host "============================================"
Write-Host " CoffeeBeanery Performance Benchmarks"
Write-Host "============================================"
Write-Host ""

$Suites = @(
    "query",
    "mutation",
    "update"
)

foreach ($suite in $Suites) {
    $script = Join-Path $PipelineRoot "pipelines\$suite.ps1"

    Write-Host ""
    Write-Host ">>> STARTING $suite"
    Write-Host ""

    & $script

    if ($LASTEXITCODE -ne 0) {
        throw "$suite benchmark failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host ">>> $suite completed"
}

Write-Host ""
Write-Host "============================================"
Write-Host " All benchmark suites completed"
Write-Host "============================================"
