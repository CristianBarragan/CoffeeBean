$ErrorActionPreference = "Stop"
$ComposeFile = Join-Path $PSScriptRoot "..\compose\update.yml"
$ProjectName = "coffeebeanery-update"

Write-Host ""
Write-Host "============================================"
Write-Host " CoffeeBeanery Update benchmark"
Write-Host "============================================"
Write-Host ""

try {
    docker compose -p $ProjectName -f $ComposeFile up -d --build
    if ($LASTEXITCODE -ne 0) { throw "Failed to start update benchmark environment." }

    docker compose -p $ProjectName -f $ComposeFile wait loader
    $code = docker inspect -f '{{.State.ExitCode}}' "${ProjectName}-loader-1"
    if (-not $code) { $code = 1 }
    exit ([int]$code)
}
finally {
    Write-Host ""
    Write-Host "Stopping update benchmark environment..."
    docker compose -p $ProjectName -f $ComposeFile down
}
