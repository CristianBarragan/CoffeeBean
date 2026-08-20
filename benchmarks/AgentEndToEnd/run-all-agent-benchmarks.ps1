using namespace System.Globalization

[CmdletBinding()]
param(
    [ValidateSet("replay", "live")]
    [string]$Mode = "replay",

    [int]$Warmups = 5,
    [int]$Runs = 30,

    [string]$CustomerCounts = "10,100,1000,10000",
    [string]$Concurrency = "8,16,32,64",

    [switch]$SkipRun1,
    [switch]$SkipRun2,
    [switch]$SkipRun3,
    [switch]$SkipRun4,
    [switch]$SkipRun5,
    [switch]$SkipRun5SameClient,
    [switch]$SkipGuardRail,

    [switch]$Publish,
    [switch]$ContinueOnError,
    [switch]$FailFast
)

$ErrorActionPreference = "Stop"

# A suite run is intended to collect every independent benchmark. By default,
# one failed run does not prevent later runs from completing. Use -FailFast to
# stop immediately. -ContinueOnError remains accepted for backward compatibility.
$ContinueOnError = $true
if ($FailFast) { $ContinueOnError = $false }

$Root = $PSScriptRoot
$Run1 = Join-Path $Root "Run1"
$Run2 = Join-Path $Root "Run2"
$Run3 = Join-Path $Root "Run3"
$Run4 = Join-Path $Root "Run4"
$Run5 = Join-Path $Root "Run5"
$Run5SameClient = Join-Path $Root "Run5SameClient"

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$SuiteLogDir = Join-Path $Root "artifacts\all-runs\$Timestamp"
New-Item -ItemType Directory -Force -Path $SuiteLogDir | Out-Null

function Write-Section([string]$Text) {
    Write-Host ""
    Write-Host ("=" * 72)
    Write-Host $Text
    Write-Host ("=" * 72)
}

function Invoke-BenchmarkScript {
    param(
        [string]$Name,
        [string]$WorkingDirectory,
        [string]$Script,
        [string[]]$Arguments
    )

    $log = Join-Path $SuiteLogDir "$Name.log"

    if (-not (Test-Path (Join-Path $WorkingDirectory $Script))) {
        throw "$Name script not found: $(Join-Path $WorkingDirectory $Script)"
    }

    Write-Host "[$Name] Working directory: $WorkingDirectory"
    Write-Host "[$Name] Log: $log"
    Write-Host "[$Name] Command: .\$Script $($Arguments -join ' ')"

    Push-Location $WorkingDirectory
    try {
        # IMPORTANT: this must stream output live, not buffer it.
        # The previous implementation used Start-Process -Wait with
        # -RedirectStandardOutput/-RedirectStandardError pointed at files, and
        # only read those files back AFTER the child process had already
        # exited. That means nothing appeared on screen for the entire
        # duration of a run (Docker builds, warmups, measured runs can easily
        # take minutes) - it looked hung even though it was working, and you
        # had no way to tell progress from a genuine stall.
        #
        # Instead, invoke the child script directly and pipe its merged
        # stdout+stderr straight through so each line is written to the
        # console and the log file as soon as it's produced. Docker Compose
        # writes progress to stderr, and $ErrorActionPreference = "Stop" at
        # the top of this file would otherwise turn every redirected stderr
        # line into a terminating error the instant one showed up - so the
        # override to "Continue" is scoped to just this call and restored
        # immediately after.
        $scriptPath = Join-Path $WorkingDirectory $Script
        $previousEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath @Arguments 2>&1 |
                ForEach-Object {
                    $line = $_.ToString()
                    Write-Host $line
                    [System.IO.File]::AppendAllText($log, $line + [Environment]::NewLine)
                }
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousEap
        }

        if ($exitCode -ne 0) {
            throw "$Name failed with exit code $exitCode. See $log"
        }

        Write-Host "[$Name] completed successfully."
        return $true
    }
    catch {
        Write-Warning "[$Name] FAILED: $($_.Exception.Message)"
        if (-not $ContinueOnError) {
            throw
        }
        return $false
    }
    finally {
        Pop-Location
    }
}

Write-Section "Foundgine Agent End-to-End Benchmark Suite"

Write-Section "GUARD RAIL - 1 customer / 1 concurrency / 1 warmup / 1 measured run"

if (-not $SkipGuardRail) {
    Write-Host "The full benchmark suite will NOT start until this smoke suite passes." -ForegroundColor Yellow
    Write-Host "Customers: 1 | Concurrency: 1 | Warmups: 1 | Measured: 1" -ForegroundColor Yellow

    $guardResults = [ordered]@{}
    $guardArgsBase = @(
        "-CustomerCounts", "1",
        "-Concurrency", "1",
        "-Runs", "1",
        "-Warmups", "1"
    )

    # Run the same wrappers used by the full suite. This catches compile,
    # container startup, GraphQL/MCP protocol, database, execution and report
    # failures before spending time on the large performance matrix.
    if (-not $SkipRun1) {
        $guardResults.Run1 = Invoke-BenchmarkScript -Name "GuardRail-Run1" -WorkingDirectory $Run1 -Script "run-agent-benchmark.ps1" -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }
    if (-not $SkipRun2) {
        $guardResults.Run2 = Invoke-BenchmarkScript -Name "GuardRail-Run2" -WorkingDirectory $Run2 -Script "run-agent-benchmark.ps1" -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }
    if (-not $SkipRun3) {
        $guardResults.Run3 = Invoke-BenchmarkScript -Name "GuardRail-Run3" -WorkingDirectory $Run3 -Script "run-agent-benchmark.ps1" -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }
    if (-not $SkipRun4) {
        $guardResults.Run4 = Invoke-BenchmarkScript -Name "GuardRail-Run4" -WorkingDirectory $Run4 -Script "run-agent-benchmark.ps1" -Arguments (@("-Mode", "both") + $guardArgsBase)
    }
    if (-not $SkipRun5) {
        $guardResults.Run5 = Invoke-BenchmarkScript -Name "GuardRail-Run5" -WorkingDirectory $Run5 -Script "run-agent-benchmark.ps1" -Arguments $guardArgsBase
    }
    if (-not $SkipRun5SameClient) {
        $guardResults.Run5SameClient = Invoke-BenchmarkScript -Name "GuardRail-Run5SameClient" -WorkingDirectory $Run5SameClient -Script "run-agent-benchmark.ps1" -Arguments $guardArgsBase
    }

    if (($guardResults.Values | Where-Object { $_ -eq $false }).Count -gt 0) {
        throw "Benchmark guard rail FAILED. Full performance matrix was not started."
    }

    Write-Host "GUARD RAIL PASSED - starting full benchmark matrix." -ForegroundColor Green
}
else {
    Write-Host "WARNING: benchmark guard rail skipped by -SkipGuardRail." -ForegroundColor Yellow
}


Write-Host "Root:            $Root"
Write-Host "Mode:            $Mode"
Write-Host "Warmups:         $Warmups"
Write-Host "Measured runs:   $Runs"
Write-Host "Customers:       $CustomerCounts"
Write-Host "Concurrency:     $Concurrency"
Write-Host "Publish:         $Publish"
Write-Host "Continue errors: $ContinueOnError"
Write-Host "Fail fast:       $FailFast"
Write-Host "Suite logs:      $SuiteLogDir"

# IMPORTANT:
# This orchestrator deliberately does NOT start a common set of Docker services.
# Each Run owns its own compose project, fixture, ports, startup/readiness,
# telemetry, and cleanup. Runs execute sequentially so their containers and
# PostgreSQL volumes cannot interfere with each other.
#
# Also remove stale benchmark build output before starting. Older merged
# workspaces contained nested bin/obj trees; if those are left around they can
# be picked up by a parent benchmark project and produce duplicate type and
# generated-attribute errors unrelated to the real project being run.
function Remove-StaleBenchmarkBuildOutput {
    $roots = @(
        $Root,
        (Join-Path $Root '..\CoffeeBeanery.Performance')
    )

    foreach ($path in $roots) {
        if (-not (Test-Path $path)) { continue }

        Get-ChildItem -LiteralPath $path -Recurse -Directory -Force |
            Where-Object { $_.Name -in @('bin', 'obj') } |
            Sort-Object FullName -Descending |
            ForEach-Object {
                Write-Host "[clean] Removing $($_.FullName)" -ForegroundColor DarkGray
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
    }
}

Remove-StaleBenchmarkBuildOutput

$results = [ordered]@{}

# ---------------------------------------------------------------------------
# Run 1
# ---------------------------------------------------------------------------
if (-not $SkipRun1) {
    Write-Section "RUN 1 - Agent End-to-End baseline"

    $args = @(
        "-Mode", $Mode,
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run1 = Invoke-BenchmarkScript `
        -Name "Run1" `
        -WorkingDirectory $Run1 `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Run 2
# ---------------------------------------------------------------------------
if (-not $SkipRun2) {
    Write-Section "RUN 2 - Agent scalability / customer tiers"

    # Run2\run-agent-benchmark.ps1 takes -Runs (a single count applied to every
    # tier) and builds its own -RunsPerTier array internally before calling
    # run-agent-end-to-end.ps1. Do NOT pass -RunsPerTier here; that parameter
    # does not exist on this wrapper and the call would fail to bind.
    #
    # CustomerCounts/Concurrency are passed as single comma-joined string
    # tokens (e.g. "-CustomerCounts" "10,100,1000,10000") because this
    # process boundary is a brand-new `powershell.exe -File ...` invocation.
    # Splitting them into separate space-separated tokens does NOT work here
    # - PowerShell only binds the first token to the array parameter and
    # errors on the rest as unmatched positional arguments. Run2's wrapper
    # now declares CustomerCounts/Concurrency as plain strings and splits
    # them internally, so a single joined token is exactly what it expects.
    $args = @(
        "-Mode", $Mode,
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run2 = Invoke-BenchmarkScript `
        -Name "Run2" `
        -WorkingDirectory $Run2 `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Run 3
# ---------------------------------------------------------------------------
if (-not $SkipRun3) {
    Write-Section "RUN 3 - Agent cost / efficiency benchmark"

    $args = @(
        "-Mode", $Mode,
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run3 = Invoke-BenchmarkScript `
        -Name "Run3" `
        -WorkingDirectory $Run3 `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Run 4
# ---------------------------------------------------------------------------
if (-not $SkipRun4) {
    Write-Section "RUN 4 - MCP + Foundgine vs Hot Chocolate + EF Core"

    # Same fix as Run 2: Run4\run-agent-benchmark.ps1 takes -Runs, not
    # -RunsPerTier, and it now declares CustomerCounts/Concurrency as plain
    # strings it splits internally - so pass single comma-joined tokens
    # here, not separate space-separated ones (see the Run 2 comment above
    # for why splitting into separate tokens fails across this process
    # boundary).
    $args = @(
        "-Mode", "both",
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run4 = Invoke-BenchmarkScript `
        -Name "Run4" `
        -WorkingDirectory $Run4 `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Run 5
# ---------------------------------------------------------------------------
if (-not $SkipRun5) {
    Write-Section "RUN 5 - High-assurance TransferFunds: MCP + EF Core vs Foundgine"

    $args = @(
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run5 = Invoke-BenchmarkScript `
        -Name "Run5" `
        -WorkingDirectory $Run5 `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Run 5 Same Client
# ---------------------------------------------------------------------------
if (-not $SkipRun5SameClient) {
    Write-Section "RUN 5 Same Client - identical Run 5 client path"

    $args = @(
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run5SameClient = Invoke-BenchmarkScript `
        -Name "Run5SameClient" `
        -WorkingDirectory $Run5SameClient `
        -Script "run-agent-benchmark.ps1" `
        -Arguments $args
}

# ---------------------------------------------------------------------------
# Publish
# ---------------------------------------------------------------------------
if ($Publish) {
    Write-Section "PUBLISH - Consolidate all benchmark reports"

    $publish = Join-Path $Root "publish-all-reports.ps1"

    if (-not (Test-Path $publish)) {
        throw "Common publisher not found: $publish"
    }

    Push-Location $Root
    try {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass `
            -File $publish *>&1 |
            Tee-Object -FilePath (Join-Path $SuiteLogDir "publish-all-reports.log")

        if ($LASTEXITCODE -ne 0) {
            throw "publish-all-reports.ps1 failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# Suite summary
# ---------------------------------------------------------------------------
Write-Section "SUITE COMPLETE"

foreach ($entry in $results.GetEnumerator()) {
    $status = if ($entry.Value) { "PASS" } else { "FAIL" }
    Write-Host ("{0,-8} {1}" -f $entry.Key, $status)
}

Write-Host ""
Write-Host "Suite logs: $SuiteLogDir"

$summary = [ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString([CultureInfo]::InvariantCulture.DateTimeFormat.SortableDateTimePattern)
    mode = $Mode
    warmups = $Warmups
    runs = $Runs
    customerCounts = $CustomerCounts
    concurrency = $Concurrency
    publish = [bool]$Publish
    results = $results
}

$summaryJson = $summary | ConvertTo-Json -Depth 10
$summaryPath = Join-Path $SuiteLogDir 'suite-summary.json'
Set-Content -Path $summaryPath -Value $summaryJson -Encoding UTF8

if (($results.Values | Where-Object { $_ -eq $false }).Count -gt 0) {
    exit 1
}
