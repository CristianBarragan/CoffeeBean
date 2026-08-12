$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$BenchmarkRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoRoot = (Resolve-Path (Join-Path $BenchmarkRoot "..\..")).Path

$ComposeFile = Join-Path $BenchmarkRoot "compose\postgres.yml"
$Network = "coffeebeanery-query_default"

$DbImage = "coffeebeanery-query-database"
$HcImage = "coffeebeanery-query-hotchocolate"
$FgImage = "coffeebeanery-query-foundgine"

$ReportRoot = Join-Path $BenchmarkRoot "reports\query"
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$ConnectionString = "Host=postgres;Port=5432;Database=foundgine_benchmark;Username=benchmark;Password=benchmark"

function Invoke-Checked {
    param(
        [Parameter(Mandatory=$true)][string]$File,
        [Parameter(Mandatory=$false)][string[]]$Arguments = @()
    )

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $File $($Arguments -join ' ')"
    }
}

function Wait-Http {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                return
            }
        } catch {
            # API is still starting.
        }

        Start-Sleep -Seconds 1
    }

    throw "API did not become ready within $TimeoutSeconds seconds: $Url"
}

function Stop-ContainerIfExists {
    param([string]$Name)

    $exists = docker ps -a --format "{{.Names}}" | Where-Object { $_ -eq $Name }
    if ($exists) {
        docker rm -f $Name | Out-Null
    }
}

function Build-Images {
    Write-Host "Building benchmark images..."

    Invoke-Checked docker @(
        "build",
        "-f", "benchmarks/CoffeeBeanery.Performance/CoffeeBeanery.Database/Dockerfile",
        "-t", $DbImage,
        "."
    )

    Invoke-Checked docker @(
        "build",
        "-f", "benchmarks/CoffeeBeanery.Performance/HotChocolate.CoffeeBeanery.BenchmarkApi/Dockerfile",
        "-t", $HcImage,
        "."
    )

    Invoke-Checked docker @(
        "build",
        "-f", "benchmarks/CoffeeBeanery.Performance/Foundgine.CoffeeBeanery.BenchmarkApi/Dockerfile",
        "-t", $FgImage,
        "."
    )
}

function Start-Database {
    Write-Host "`nStarting PostgreSQL..."
    Invoke-Checked docker @("compose", "-f", $ComposeFile, "up", "-d", "--wait")

    Write-Host "Running database migration and fixture seeding..."
    Stop-ContainerIfExists "coffeebeanery-query-database"

    Invoke-Checked docker @(
        "run", "--rm",
        "--name", "coffeebeanery-query-database",
        "--network", $Network,
        "-e", "BankingConnectionString=$ConnectionString",
        "-e", "COFFEEBEANERY_CONNECTION=$ConnectionString",
        "-e", "ConnectionStrings__BankingConnectionString=$ConnectionString",
        "-e", "COFFEEBEANERY_CUSTOMERS=1000",
        "-e", "COFFEEBEANERY_RELATIONSHIPS_PER_CUSTOMER=4",
        "-e", "COFFEEBEANERY_CONTRACTS_PER_RELATIONSHIP=3",
        "-e", "COFFEEBEANERY_TRANSACTIONS_PER_CONTRACT=4",
        $DbImage
    )

    Write-Host "Database initialization completed successfully."
}

function Start-Api {
    param(
        [string]$ContainerName,
        [string]$Image,
        [int]$Port
    )

    Stop-ContainerIfExists $ContainerName

    Write-Host "Starting $ContainerName..."
    Invoke-Checked docker @(
        "run", "-d",
        "--name", $ContainerName,
        "--network", $Network,
        "-p", "${Port}:4300",
        "-e", "ConnectionStrings__BankingConnectionString=$ConnectionString",
        "-e", "BankingConnectionString=$ConnectionString",
        "-e", "ASPNETCORE_URLS=http://+:4300",
        "-e", "ASPNETCORE_ENVIRONMENT=Production",
        $Image
    )

    Wait-Http "http://localhost:$Port/health" 120
    Write-Host "$ContainerName is ready."
}

function Run-LoadTest {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Directory
    )

    $reportDirectory = Join-Path $ReportRoot $Directory
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

    Write-Host "`nRunning benchmark: $Name"

    $env:BENCHMARK_TARGET_NAME = $Name
    $env:BENCHMARK_TARGET_URL = $Url
    $env:BENCHMARK_WARMUP_SECONDS = "3"
    $env:BENCHMARK_DURATION_SECONDS = "10"
    $env:BENCHMARK_REQUEST_TIMEOUT_SECONDS = "5"
    $env:BENCHMARK_READINESS_TIMEOUT_SECONDS = "120"
    $env:BENCHMARK_RESET_TIMEOUT_SECONDS = "30"
    $env:BENCHMARK_CONCURRENCY = "1,8,32"
    $env:BENCHMARK_REPORT_DIRECTORY = $reportDirectory

    Invoke-Checked dotnet @(
        "run",
        "--project",
        (Join-Path $BenchmarkRoot "CoffeeBeanery.LoadTest\CoffeeBeanery.LoadTest.csproj"),
        "--configuration",
        "Release",
        "--no-launch-profile"
    )

    Remove-Item Env:BENCHMARK_TARGET_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:BENCHMARK_TARGET_URL -ErrorAction SilentlyContinue
}

function Stop-Api {
    param([string]$ContainerName)

    Write-Host "Stopping $ContainerName..."
    docker rm -f $ContainerName 2>$null | Out-Null
}

try {
    Set-Location $RepoRoot

    Build-Images
    Start-Database

    # Exactly one API is alive during each measurement.
    Start-Api "coffeebeanery-query-hotchocolate" $HcImage 4300
    try {
        Run-LoadTest "Hot Chocolate + EF Core" "http://localhost:4300/graphql" "hotchocolate"
    }
    finally {
        Stop-Api "coffeebeanery-query-hotchocolate"
    }

    Start-Api "coffeebeanery-query-foundgine-cold" $FgImage 4301
    try {
        Run-LoadTest "Foundgine - no cache" "http://localhost:4301/graphql/cold" "foundgine-cold"
    }
    finally {
        Stop-Api "coffeebeanery-query-foundgine-cold"
    }

    Start-Api "coffeebeanery-query-foundgine-warm" $FgImage 4302
    try {
        Run-LoadTest "Foundgine - provider-plan cache" "http://localhost:4302/graphql/warm" "foundgine-warm"
    }
    finally {
        Stop-Api "coffeebeanery-query-foundgine-warm"
    }

    Write-Host "`nQuery benchmark completed."
    Write-Host "Reports: $ReportRoot"
}
finally {
    docker rm -f `
        "coffeebeanery-query-hotchocolate" `
        "coffeebeanery-query-foundgine-cold" `
        "coffeebeanery-query-foundgine-warm" `
        "coffeebeanery-query-database" 2>$null | Out-Null

    docker compose -f $ComposeFile down 2>$null | Out-Null
}
