$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir '..\..\..')).Path

Set-Location $root

$bench = 'benchmarks/CoffeeBeanery.Performance'

$compose = "$bench/docker-compose.benchmark.yml"

$db = "$bench/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj"

$hc = "$bench/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj"

$foundgine = "$bench/Foundgine.CoffeeBeanery.BenchmarkApi/Foundgine.CoffeeBeanery.BenchmarkApi.csproj"

$load = "$bench/CoffeeBeanery.LoadTest/CoffeeBeanery.LoadTest.csproj"

$conn = 'Host=localhost;Port=55432;Database=foundgine_benchmark;Username=benchmark;Password=benchmark'


# ============================================================
# ENVIRONMENT
# ============================================================

$env:COFFEEBEANERY_CONNECTION = $conn
$env:ConnectionStrings__BankingConnectionString = $conn

if (-not $env:BENCHMARK_CUSTOMER_ID) {
    $env:BENCHMARK_CUSTOMER_ID = '1'
}

if (-not $env:BENCHMARK_DURATION_SECONDS) {
    $env:BENCHMARK_DURATION_SECONDS = '30'
}

if (-not $env:BENCHMARK_WARMUP_SECONDS) {
    $env:BENCHMARK_WARMUP_SECONDS = '10'
}

if (-not $env:BENCHMARK_CONCURRENCY) {
    $env:BENCHMARK_CONCURRENCY = '1,8,16,32,64'
}

if (-not $env:COFFEEBEANERY_CUSTOMERS) {
    $env:COFFEEBEANERY_CUSTOMERS = '1000'
}

if (-not $env:COFFEEBEANERY_RELATIONSHIPS_PER_CUSTOMER) {
    $env:COFFEEBEANERY_RELATIONSHIPS_PER_CUSTOMER = '4'
}

if (-not $env:COFFEEBEANERY_CONTRACTS_PER_RELATIONSHIP) {
    $env:COFFEEBEANERY_CONTRACTS_PER_RELATIONSHIP = '3'
}

if (-not $env:COFFEEBEANERY_TRANSACTIONS_PER_CONTRACT) {
    $env:COFFEEBEANERY_TRANSACTIONS_PER_CONTRACT = '4'
}


# ============================================================
# PROCESS STATE
# ============================================================

$hcProcess = $null
$foundgineProcess = $null


# ============================================================
# HELPERS
# ============================================================

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $false)]
        [string[]]$Arguments = @()
    )

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}


function Wait-ForUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    Write-Host "Waiting for $Url"

    for ($i = 0; $i -lt 60; $i++) {
        try {
            Invoke-WebRequest `
                -Uri $Url `
                -UseBasicParsing `
                -TimeoutSec 2 | Out-Null

            return
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "ERROR: $Url did not become ready."
}


# ============================================================
# MAIN
# ============================================================

try {

    Write-Host ""
    Write-Host "=============================================="
    Write-Host " CoffeeBeanery / Foundgine Performance Run"
    Write-Host "=============================================="
    Write-Host "Repository:  $root"
    Write-Host "Compose:     $compose"
    Write-Host "Database:    $conn"
    Write-Host "Customer:    $env:BENCHMARK_CUSTOMER_ID"
    Write-Host "Warm-up:     $($env:BENCHMARK_WARMUP_SECONDS)s"
    Write-Host "Measurement: $($env:BENCHMARK_DURATION_SECONDS)s"
    Write-Host "Concurrency: $env:BENCHMARK_CONCURRENCY"
    Write-Host ""

    # ========================================================
    # RESTORE
    # ========================================================

    Write-Host "== Restore performance projects =="

    Invoke-Checked 'dotnet' @(
        'restore',
        $db
    )

    Invoke-Checked 'dotnet' @(
        'restore',
        $hc
    )

    Invoke-Checked 'dotnet' @(
        'restore',
        $foundgine
    )

    Invoke-Checked 'dotnet' @(
        'restore',
        $load
    )


    # ========================================================
    # INSTALL EF CORE CLI
    #
    # DO NOT use:
    #
    #   dotnet tool restore
    #   dotnet tool run dotnet-ef
    #
    # There is no dotnet-tools.json manifest in the repository.
    #
    # Install a deterministic local copy instead.
    # ========================================================

    Write-Host ""
    Write-Host "== Install pinned EF Core CLI =="

    $toolDir = Join-Path $bench '.tools'

    if (Test-Path $toolDir) {
        Remove-Item `
            $toolDir `
            -Recurse `
            -Force
    }

    New-Item `
        -ItemType Directory `
        -Path $toolDir `
        -Force | Out-Null

    Invoke-Checked 'dotnet' @(
        'tool',
        'install',
        'dotnet-ef',
        '--tool-path',
        $toolDir,
        '--version',
        '9.0.7'
    )

    $efTool = Join-Path $toolDir 'dotnet-ef.exe'

    if (-not (Test-Path $efTool)) {
        throw "dotnet-ef was not installed at $efTool"
    }

    Write-Host "EF Core CLI: $efTool"


    # ========================================================
    # POSTGRESQL
    # ========================================================

    Write-Host ""
    Write-Host "== Start fresh PostgreSQL fixture =="

    # Docker compose writes routine, non-error progress lines ("Removing",
    # "Removed", ...) to stderr. Piping that directly through PowerShell's
    # native-command handling (even with 2>$null, which only redirects the
    # stream - $PSNativeCommandUseErrorActionPreference reacts to exit code,
    # not stream content) has been observed to crash the pwsh host outright
    # on some runners. Route it through cmd.exe instead, exactly like the
    # cleanup call below and Stop-AllBenchmarkEnvironments in
    # run-benchmarks.ps1, so PowerShell never reads docker's raw output.
    & cmd.exe /d /c "docker compose -f `"$compose`" down -v --remove-orphans >nul 2>&1"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "No existing benchmark environment to remove."
    }

    Invoke-Checked 'docker' @(
        'compose',
        '-f',
        $compose,
        'up',
        '-d',
        '--wait'
    )


    # ========================================================
    # EF CORE SCHEMA
    # ========================================================

    Write-Host ""
    Write-Host "== Apply CoffeeBeanery EF Core schema =="

    Invoke-Checked $efTool @(
        'database',
        'update',
        '--project',
        $db,
        '--startup-project',
        $db,
        '--context',
        'BankingEntityContext',
        '--configuration',
        'Release'
    )


    # ========================================================
    # DETERMINISTIC FIXTURE
    # ========================================================

    Write-Host ""
    Write-Host "== Seed deterministic CoffeeBeanery data =="

    Invoke-Checked 'dotnet' @(
        'run',
        '--project',
        $db,
        '--configuration',
        'Release',
        '--no-restore'
    )


    # ========================================================
    # HOT CHOCOLATE
    # ========================================================

    Write-Host ""
    Write-Host "== Start Hot Chocolate API =="

    $hcLog = Join-Path `
        $env:TEMP `
        'foundgine-hc.log'

    if (Test-Path $hcLog) {
        Remove-Item `
            $hcLog `
            -Force `
            -ErrorAction SilentlyContinue
    }

    $hcProcess = Start-Process `
        dotnet `
        -ArgumentList @(
            'run',
            '--project',
            $hc,
            '--configuration',
            'Release',
            '--no-restore',
            '--urls',
            'http://localhost:4300'
        ) `
        -RedirectStandardOutput $hcLog `
        -RedirectStandardError $hcLog `
        -PassThru


    # ========================================================
    # FOUNDGINE
    # ========================================================

    Write-Host ""
    Write-Host "== Start Foundgine API =="

    $fgLog = Join-Path `
        $env:TEMP `
        'foundgine-foundgine.log'

    if (Test-Path $fgLog) {
        Remove-Item `
            $fgLog `
            -Force `
            -ErrorAction SilentlyContinue
    }

    $foundgineProcess = Start-Process `
        dotnet `
        -ArgumentList @(
            'run',
            '--project',
            $foundgine,
            '--configuration',
            'Release',
            '--no-restore',
            '--urls',
            'http://localhost:4301'
        ) `
        -RedirectStandardOutput $fgLog `
        -RedirectStandardError $fgLog `
        -PassThru


    # ========================================================
    # READINESS
    # ========================================================

    Write-Host ""
    Write-Host "== Wait for APIs =="

    Wait-ForUrl 'http://localhost:4300/health'

    Wait-ForUrl 'http://localhost:4301/health'


    # ========================================================
    # LOAD TEST
    # ========================================================

    Write-Host ""
    Write-Host "== Run HTTP performance benchmark =="

    Invoke-Checked 'dotnet' @(
        'run',
        '--project',
        $load,
        '--configuration',
        'Release',
        '--no-restore'
    )


    Write-Host ""
    Write-Host "=============================================="
    Write-Host " Benchmark completed successfully"
    Write-Host "=============================================="
}
finally {

    Write-Host ""
    Write-Host "== Performance cleanup =="

    if ($hcProcess -and -not $hcProcess.HasExited) {
        Stop-Process `
            -Id $hcProcess.Id `
            -Force `
            -ErrorAction SilentlyContinue
    }

    if ($foundgineProcess -and -not $foundgineProcess.HasExited) {
        Stop-Process `
            -Id $foundgineProcess.Id `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Write-Host "== Docker cleanup =="

    & cmd.exe /d /c "docker compose -f `"$compose`" down -v --remove-orphans >nul 2>&1" | Out-Null

    Write-Host "== Performance environment destroyed =="
}