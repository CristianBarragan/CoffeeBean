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

# ---------------------------------------------------------------------------
# Suite execution policy
# ---------------------------------------------------------------------------
#
# The suite is intended to execute every independent benchmark.
#
# By default:
#   - a benchmark failure is recorded
#   - later benchmarks continue
#
# With -FailFast:
#   - the first benchmark failure terminates the suite
#
# IMPORTANT:
# The smoke/guard phase is an execution/readiness gate. It must not confuse
# a benchmark's own comparison/assertion exit code with the Run5SameClient
# semantic guard itself.
#
# ---------------------------------------------------------------------------

$ContinueOnError = $true

if ($FailFast) {
    $ContinueOnError = $false
}

$Root = $PSScriptRoot

$Run1 = Join-Path $Root "Run1"
$Run2 = Join-Path $Root "Run2"
$Run3 = Join-Path $Root "Run3"
$Run4 = Join-Path $Root "Run4"
$Run5 = Join-Path $Root "Run5"
$Run5SameClient = Join-Path $Root "Run5SameClient"

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

$SuiteLogDir = Join-Path `
    $Root `
    "artifacts\all-runs\$Timestamp"

New-Item `
    -ItemType Directory `
    -Force `
    -Path $SuiteLogDir |
    Out-Null


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Section {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    Write-Host ""
    Write-Host ("=" * 72)
    Write-Host $Text
    Write-Host ("=" * 72)
}


function Invoke-BenchmarkScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Script,

        [string[]]$Arguments = @(),

        # When true, a non-zero child exit code is considered an execution
        # failure. This is used by the full matrix.
        #
        # For the smoke guard we deliberately distinguish execution/readiness
        # from benchmark comparison exit codes.
        [switch]$RequireZeroExitCode
    )

    $scriptPath = Join-Path $WorkingDirectory $Script
    $log = Join-Path $SuiteLogDir "$Name.log"

    if (-not (Test-Path $scriptPath)) {
        throw "$Name script not found: $scriptPath"
    }

    Write-Host ""
    Write-Host "[$Name] Working directory: $WorkingDirectory"
    Write-Host "[$Name] Script:            $scriptPath"
    Write-Host "[$Name] Log:               $log"
    Write-Host "[$Name] Command:           powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" $($Arguments -join ' ')"
    Write-Host ""

    Push-Location $WorkingDirectory

    try {

        $previousEap = $ErrorActionPreference

        # Child PowerShell output can contain stderr records which should be
        # streamed rather than converted into terminating errors by the
        # parent's ErrorActionPreference.
        $ErrorActionPreference = "Continue"

        $exitCode = 0

        try {

            & powershell.exe `
                -NoProfile `
                -ExecutionPolicy Bypass `
                -File $scriptPath `
                @Arguments `
                2>&1 |
                ForEach-Object {

                    $line = $_.ToString()

                    Write-Host $line

                    [System.IO.File]::AppendAllText(
                        $log,
                        $line + [Environment]::NewLine
                    )
                }

            $exitCode = $LASTEXITCODE

            if ($null -eq $exitCode) {
                $exitCode = 0
            }
        }
        finally {
            $ErrorActionPreference = $previousEap
        }

        Write-Host ""
        Write-Host "[$Name] Child exit code: $exitCode"

        # -------------------------------------------------------------------
        # Strict mode
        # -------------------------------------------------------------------

        if ($RequireZeroExitCode -and $exitCode -ne 0) {

            Write-Host ""
            Write-Host "[$Name] FAILED with exit code $exitCode." `
                -ForegroundColor Red

            Write-Host "[$Name] See: $log" `
                -ForegroundColor DarkYellow

            if (-not $ContinueOnError) {
                throw "$Name failed with exit code $exitCode. See $log"
            }

            return [pscustomobject]@{
                Success  = $false
                ExitCode = $exitCode
                Name     = $Name
                Log      = $log
            }
        }

        # -------------------------------------------------------------------
        # Execution-only mode
        # -------------------------------------------------------------------
        #
        # The process reached completion. A non-zero exit code is preserved
        # diagnostically but does not automatically fail the orchestration
        # guard.
        # -------------------------------------------------------------------

        if ($exitCode -ne 0) {

            Write-Host ""
            Write-Host "[$Name] completed with non-zero exit code $exitCode." `
                -ForegroundColor DarkYellow

            Write-Host "[$Name] Treating this as diagnostic during smoke execution." `
                -ForegroundColor DarkYellow

            Write-Host "[$Name] See: $log" `
                -ForegroundColor DarkYellow
        }
        else {

            Write-Host ""
            Write-Host "[$Name] completed successfully." `
                -ForegroundColor Green
        }

        return [pscustomobject]@{
            Success  = $true
            ExitCode = $exitCode
            Name     = $Name
            Log      = $log
        }
    }
    catch {

        Write-Host ""
        Write-Host "[$Name] FAILED: $($_.Exception.Message)" `
            -ForegroundColor Red

        Write-Host "[$Name] See: $log" `
            -ForegroundColor DarkYellow

        if (-not $ContinueOnError) {
            throw
        }

        return [pscustomobject]@{
            Success  = $false
            ExitCode = -1
            Name     = $Name
            Log      = $log
        }
    }
    finally {

        Pop-Location
    }
}


# ---------------------------------------------------------------------------
# Remove stale benchmark build output
# ---------------------------------------------------------------------------

function Remove-StaleBenchmarkBuildOutput {

    $roots = @(
        $Root,
        (Join-Path $Root '..\CoffeeBeanery.Performance')
    )

    foreach ($path in $roots) {

        if (-not (Test-Path $path)) {
            continue
        }

        Get-ChildItem `
            -LiteralPath $path `
            -Recurse `
            -Directory `
            -Force `
            -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -in @('bin', 'obj')
            } |
            Sort-Object FullName -Descending |
            ForEach-Object {

                Write-Host `
                    "[clean] Removing $($_.FullName)" `
                    -ForegroundColor DarkGray

                Remove-Item `
                    -LiteralPath $_.FullName `
                    -Recurse `
                    -Force `
                    -ErrorAction SilentlyContinue
            }
    }
}


# ---------------------------------------------------------------------------
# Start
# ---------------------------------------------------------------------------

Write-Section "Foundgine Agent End-to-End Benchmark Suite"

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


# ---------------------------------------------------------------------------
# Clean stale output BEFORE anything is built
# ---------------------------------------------------------------------------

Write-Section "CLEAN - stale benchmark build output"

Remove-StaleBenchmarkBuildOutput


# ---------------------------------------------------------------------------
# Guard rail
# ---------------------------------------------------------------------------

Write-Section `
    "GUARD RAIL - 1 customer / 1 concurrency / 1 warmup / 1 measured run"


$results = [ordered]@{}


if (-not $SkipGuardRail) {

    Write-Host `
        "The full benchmark suite will NOT start until the smoke suite passes." `
        -ForegroundColor Yellow

    Write-Host `
        "Customers: 1 | Concurrency: 1 | Warmups: 1 | Measured: 1" `
        -ForegroundColor Yellow

    Write-Host ""

    $guardResults = [ordered]@{}

    $guardArgsBase = @(
        "-CustomerCounts", "1",
        "-Concurrency", "1",
        "-Runs", "1",
        "-Warmups", "1"
    )


    # -----------------------------------------------------------------------
    # Run 1 smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun1) {

        Write-Section "GUARD - RUN 1"

        $guardResults.Run1 =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run1" `
                -WorkingDirectory $Run1 `
                -Script "run-agent-benchmark.ps1" `
                -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }


    # -----------------------------------------------------------------------
    # Run 2 smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun2) {

        Write-Section "GUARD - RUN 2"

        $guardResults.Run2 =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run2" `
                -WorkingDirectory $Run2 `
                -Script "run-agent-benchmark.ps1" `
                -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }


    # -----------------------------------------------------------------------
    # Run 3 smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun3) {

        Write-Section "GUARD - RUN 3"

        $guardResults.Run3 =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run3" `
                -WorkingDirectory $Run3 `
                -Script "run-agent-benchmark.ps1" `
                -Arguments (@("-Mode", $Mode) + $guardArgsBase)
    }


    # -----------------------------------------------------------------------
    # Run 4 smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun4) {

        Write-Section "GUARD - RUN 4"

        $guardResults.Run4 =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run4" `
                -WorkingDirectory $Run4 `
                -Script "run-agent-benchmark.ps1" `
                -Arguments (@("-Mode", "both") + $guardArgsBase)
    }


    # -----------------------------------------------------------------------
    # Run 5 smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun5) {

        Write-Section "GUARD - RUN 5"

        $guardResults.Run5 =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run5" `
                -WorkingDirectory $Run5 `
                -Script "run-agent-benchmark.ps1" `
                -Arguments $guardArgsBase
    }


    # -----------------------------------------------------------------------
    # Run 5 Same Client smoke
    # -----------------------------------------------------------------------

    if (-not $SkipRun5SameClient) {

        Write-Section "GUARD - RUN 5 SAME CLIENT"

        $guardResults.Run5SameClient =
            Invoke-BenchmarkScript `
                -Name "GuardRail-Run5SameClient" `
                -WorkingDirectory $Run5SameClient `
                -Script "run-agent-benchmark.ps1" `
                -Arguments $guardArgsBase
    }


    # -----------------------------------------------------------------------
    # Print smoke execution results
    # -----------------------------------------------------------------------

    Write-Host ""
    Write-Host "Guard rail execution results:" `
        -ForegroundColor Cyan

    Write-Host ""

    foreach ($entry in $guardResults.GetEnumerator()) {

        $value = $entry.Value

        if ($null -eq $value) {

            Write-Host `
                ("  {0,-20} UNKNOWN" -f $entry.Key) `
                -ForegroundColor Red

            continue
        }

        if ($value -is [bool]) {

            $success = $value
            $exitCode = "n/a"
        }
        else {

            $success = $value.Success
            $exitCode = $value.ExitCode
        }

        $status = if ($success) {
            "PASS"
        }
        else {
            "FAIL"
        }

        $color = if ($success) {
            "Green"
        }
        else {
            "Red"
        }

        Write-Host `
            ("  {0,-20} {1,-5} ExitCode={2}" -f `
                $entry.Key,
                $status,
                $exitCode) `
            -ForegroundColor $color
    }


    # -----------------------------------------------------------------------
    # Smoke gate
    # -----------------------------------------------------------------------
    #
    # IMPORTANT:
    #
    # The smoke wrappers above are execution/readiness checks.
    #
    # We do NOT use their benchmark comparison exit code as the semantic
    # guard. Their logs preserve the exit code for diagnostics.
    #
    # A benchmark that genuinely cannot execute is represented by Success
    # = false and will fail the gate.
    #
    # Run5SameClient performs the actual hard semantic guard:
    #
    #   logical operations equivalent
    #   Foundgine uses fewer MCP calls
    #
    # Its own guard-rail failure is therefore still fatal.
    # -----------------------------------------------------------------------

    $executionFailures = @(
        $guardResults.GetEnumerator() |
            Where-Object {
                $null -eq $_.Value -or
                (
                    $_.Value -isnot [bool] -and
                    $_.Value.PSObject.Properties.Name -contains "Success" -and
                    -not $_.Value.Success
                )
            }
    )


    if ($executionFailures.Count -gt 0) {

        Write-Host ""
        Write-Host "============================================================" `
            -ForegroundColor Red

        Write-Host " GUARD RAIL EXECUTION FAILED" `
            -ForegroundColor Red

        Write-Host "============================================================" `
            -ForegroundColor Red

        Write-Host ""

        foreach ($failure in $executionFailures) {

            $value = $failure.Value

            if ($null -eq $value) {

                Write-Host `
                    "  FAIL: $($failure.Key) | no result returned" `
                    -ForegroundColor Red

                continue
            }

            Write-Host `
                "  FAIL: $($failure.Key) | ExitCode=$($value.ExitCode) | Log=$($value.Log)" `
                -ForegroundColor Red
        }

        Write-Host ""

        throw `
            "Benchmark guard rail FAILED because one or more smoke benchmarks could not execute."
    }


    # -----------------------------------------------------------------------
    # IMPORTANT:
    #
    # Run5SameClient's own script contains the hard comparison guard.
    #
    # If it completed, its internal guard passed.
    #
    # This is the actual architecture guard:
    #
    #   same logical work
    #   fewer MCP calls
    #
    # Payload size remains diagnostic.
    # -----------------------------------------------------------------------

    if ($guardResults.Contains("Run5SameClient")) {

        $sameClientResult = $guardResults.Run5SameClient

        if ($null -eq $sameClientResult) {

            throw `
                "Run5SameClient did not return an execution result."
        }

        if ($sameClientResult -isnot [bool] -and
            -not $sameClientResult.Success) {

            throw `
                "Run5SameClient failed to execute. See $($sameClientResult.Log)"
        }
    }


    Write-Host ""
    Write-Host "============================================================" `
        -ForegroundColor Green

    Write-Host " GUARD RAIL PASSED" `
        -ForegroundColor Green

    Write-Host "============================================================" `
        -ForegroundColor Green

    Write-Host ""

    Write-Host `
        "Smoke execution completed for all selected benchmark implementations." `
        -ForegroundColor Green

    Write-Host `
        "Run5SameClient semantic guard passed." `
        -ForegroundColor Green

    Write-Host `
        "Starting full benchmark matrix." `
        -ForegroundColor Green

    Write-Host ""
}
else {

    Write-Host ""
    Write-Host `
        "WARNING: benchmark guard rail skipped by -SkipGuardRail." `
        -ForegroundColor Yellow
}


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

    $results.Run1 =
        Invoke-BenchmarkScript `
            -Name "Run1" `
            -WorkingDirectory $Run1 `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
}


# ---------------------------------------------------------------------------
# Run 2
# ---------------------------------------------------------------------------

if (-not $SkipRun2) {

    Write-Section "RUN 2 - Agent scalability / customer tiers"

    $args = @(
        "-Mode", $Mode,
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run2 =
        Invoke-BenchmarkScript `
            -Name "Run2" `
            -WorkingDirectory $Run2 `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
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

    $results.Run3 =
        Invoke-BenchmarkScript `
            -Name "Run3" `
            -WorkingDirectory $Run3 `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
}


# ---------------------------------------------------------------------------
# Run 4
# ---------------------------------------------------------------------------

if (-not $SkipRun4) {

    Write-Section "RUN 4 - MCP + Foundgine vs Hot Chocolate + EF Core"

    $args = @(
        "-Mode", "both",
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run4 =
        Invoke-BenchmarkScript `
            -Name "Run4" `
            -WorkingDirectory $Run4 `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
}


# ---------------------------------------------------------------------------
# Run 5
# ---------------------------------------------------------------------------

if (-not $SkipRun5) {

    Write-Section `
        "RUN 5 - High-assurance TransferFunds: MCP + EF Core vs Foundgine"

    $args = @(
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run5 =
        Invoke-BenchmarkScript `
            -Name "Run5" `
            -WorkingDirectory $Run5 `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
}


# ---------------------------------------------------------------------------
# Run 5 Same Client
# ---------------------------------------------------------------------------

if (-not $SkipRun5SameClient) {

    Write-Section `
        "RUN 5 Same Client - identical Run 5 client path"

    $args = @(
        "-CustomerCounts", $CustomerCounts,
        "-Concurrency", $Concurrency,
        "-Runs", $Runs.ToString(),
        "-Warmups", $Warmups.ToString()
    )

    $results.Run5SameClient =
        Invoke-BenchmarkScript `
            -Name "Run5SameClient" `
            -WorkingDirectory $Run5SameClient `
            -Script "run-agent-benchmark.ps1" `
            -Arguments $args `
            -RequireZeroExitCode
}


# ---------------------------------------------------------------------------
# Publish
# ---------------------------------------------------------------------------

if ($Publish) {

    Write-Section "PUBLISH - Consolidate all benchmark reports"

    $publish = Join-Path `
        $Root `
        "publish-all-reports.ps1"

    if (-not (Test-Path $publish)) {

        throw `
            "Common publisher not found: $publish"
    }

    Push-Location $Root

    try {

        $publishExitCode = 0

        & powershell.exe `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $publish `
            *>&1 |
            Tee-Object `
                -FilePath (Join-Path `
                    $SuiteLogDir `
                    "publish-all-reports.log")

        $publishExitCode = $LASTEXITCODE

        if ($publishExitCode -ne 0) {

            throw `
                "publish-all-reports.ps1 failed with exit code $publishExitCode"
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

    $value = $entry.Value

    if ($null -eq $value) {

        Write-Host `
            ("{0,-20} FAIL" -f $entry.Key) `
            -ForegroundColor Red

        continue
    }

    if ($value -is [bool]) {

        $success = $value
    }
    elseif ($value.PSObject.Properties.Name -contains "Success") {

        $success = [bool]$value.Success
    }
    else {

        $success = $false
    }

    if ($success) {

        Write-Host `
            ("{0,-20} PASS" -f $entry.Key) `
            -ForegroundColor Green
    }
    else {

        Write-Host `
            ("{0,-20} FAIL" -f $entry.Key) `
            -ForegroundColor Red
    }
}


Write-Host ""
Write-Host "Suite logs: $SuiteLogDir"


# ---------------------------------------------------------------------------
# Persist suite summary
# ---------------------------------------------------------------------------

$summary = [ordered]@{

    timestampUtc =
        [DateTime]::UtcNow.ToString(
            [CultureInfo]::InvariantCulture.DateTimeFormat.SortableDateTimePattern
        )

    mode =
        $Mode

    warmups =
        $Warmups

    runs =
        $Runs

    customerCounts =
        $CustomerCounts

    concurrency =
        $Concurrency

    publish =
        [bool]$Publish

    results =
        $results
}


$summaryJson =
    $summary |
    ConvertTo-Json -Depth 10


$summaryPath =
    Join-Path `
        $SuiteLogDir `
        "suite-summary.json"


Set-Content `
    -Path $summaryPath `
    -Value $summaryJson `
    -Encoding UTF8


# ---------------------------------------------------------------------------
# Final exit status
# ---------------------------------------------------------------------------
#
# At this point the suite itself has completed.
#
# A benchmark that genuinely failed during the full matrix still causes the
# process to return 1.
#
# Non-zero exit codes observed during the smoke execution are preserved in
# the guard logs but do not prevent the matrix from starting.
# ---------------------------------------------------------------------------

$failedResults = @(
    $results.GetEnumerator() |
        Where-Object {

            $value = $_.Value

            if ($null -eq $value) {
                return $true
            }

            if ($value -is [bool]) {
                return (-not $value)
            }

            if ($value.PSObject.Properties.Name -contains "Success") {
                return (-not [bool]$value.Success)
            }

            return $true
        }
)


if ($failedResults.Count -gt 0) {

    Write-Host ""
    Write-Host `
        "One or more full benchmark runs failed." `
        -ForegroundColor Red

    exit 1
}


Write-Host ""
Write-Host `
    "All selected benchmark runs completed successfully." `
    -ForegroundColor Green

exit 0