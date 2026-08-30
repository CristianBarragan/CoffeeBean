$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot "..\..")

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Foundgine Agent End-to-End Benchmark Suite" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# Helpers
# ------------------------------------------------------------

function Invoke-BenchmarkScript {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [string[]] $Arguments = @()
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host " Running: $Path" -ForegroundColor DarkCyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host ""

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Path @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Benchmark script failed: $Path (exit code $LASTEXITCODE)"
    }
}

function Get-JsonPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        $Object,

        [Parameter(Mandatory = $true)]
        [string[]] $Names
    )

    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties |
            Where-Object { $_.Name -ieq $name } |
            Select-Object -First 1

        if ($null -ne $property) {
            return $property.Value
        }
    }

    return $null
}

function Convert-ToDoubleSafe {
    param(
        $Value
    )

    if ($null -eq $Value) {
        return $null
    }

    try {
        return [double]$Value
    }
    catch {
        return $null
    }
}

# ------------------------------------------------------------
# Run5SameClient guard rail
#
# IMPORTANT:
#
# We intentionally DO NOT require payload reduction here.
#
# Foundgine's architecture can legitimately produce:
#
#   fewer MCP calls
#   same logical work
#   larger semantic payload
#
# The benchmark must measure that trade-off rather than reject it.
#
# HARD GUARD RAILS:
#   1. Both implementations must complete.
#   2. Logical operation count must be equivalent.
#   3. Foundgine must use fewer MCP calls.
#
# DIAGNOSTIC ONLY:
#   - average input payload
#   - total task payload
#   - payload delta
# ------------------------------------------------------------

function Test-Run5SameClientGuardRail {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResultsDirectory
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host "[GuardRail-Run5SameClient]" -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path $ResultsDirectory)) {
        throw "Run5SameClient results directory was not found: $ResultsDirectory"
    }

    $jsonFiles = Get-ChildItem `
        -Path $ResultsDirectory `
        -Filter "*.json" `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue

    if ($null -eq $jsonFiles -or $jsonFiles.Count -eq 0) {
        throw "No Run5SameClient JSON result files were found in: $ResultsDirectory"
    }

    Write-Host "Found $($jsonFiles.Count) JSON result file(s)." -ForegroundColor Gray

    $efCoreCalls = $null
    $foundgineCalls = $null

    $efCoreOperations = $null
    $foundgineOperations = $null

    $efCorePayload = $null
    $foundginePayload = $null

    foreach ($file in $jsonFiles) {

        try {
            $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
        }
        catch {
            Write-Host "Skipping invalid JSON: $($file.FullName)" -ForegroundColor DarkYellow
            continue
        }

        # ----------------------------------------------------
        # Locate implementation name
        # ----------------------------------------------------

        $implementation = Get-JsonPropertyValue `
            -Object $json `
            -Names @(
                "Implementation",
                "implementation",
                "Mode",
                "mode",
                "Provider",
                "provider",
                "Name",
                "name"
            )

        $implementationText = if ($null -ne $implementation) {
            "$implementation"
        }
        else {
            $file.Name
        }

        # ----------------------------------------------------
        # Extract MCP calls
        # ----------------------------------------------------

        $calls = Get-JsonPropertyValue `
            -Object $json `
            -Names @(
                "McpCalls",
                "mcpCalls",
                "ToolCalls",
                "toolCalls",
                "McpToolCalls",
                "mcpToolCalls",
                "TotalMcpCalls",
                "totalMcpCalls"
            )

        # ----------------------------------------------------
        # Extract logical operations
        # ----------------------------------------------------

        $operations = Get-JsonPropertyValue `
            -Object $json `
            -Names @(
                "LogicalOperations",
                "logicalOperations",
                "LogicalOperationCount",
                "logicalOperationCount",
                "Operations",
                "operations",
                "OperationCount",
                "operationCount"
            )

        # ----------------------------------------------------
        # Extract total payload
        # ----------------------------------------------------

        $payload = Get-JsonPropertyValue `
            -Object $json `
            -Names @(
                "TotalTaskPayload",
                "totalTaskPayload",
                "TaskPayloadBytes",
                "taskPayloadBytes",
                "TotalPayloadBytes",
                "totalPayloadBytes",
                "PayloadBytes",
                "payloadBytes"
            )

        $callsValue = Convert-ToDoubleSafe $calls
        $operationsValue = Convert-ToDoubleSafe $operations
        $payloadValue = Convert-ToDoubleSafe $payload

        if ($implementationText -match "EF|EntityFramework|Conventional") {

            if ($null -ne $callsValue) {
                $efCoreCalls = $callsValue
            }

            if ($null -ne $operationsValue) {
                $efCoreOperations = $operationsValue
            }

            if ($null -ne $payloadValue) {
                $efCorePayload = $payloadValue
            }
        }
        elseif ($implementationText -match "Foundgine|Semantic") {

            if ($null -ne $callsValue) {
                $foundgineCalls = $callsValue
            }

            if ($null -ne $operationsValue) {
                $foundgineOperations = $operationsValue
            }

            if ($null -ne $payloadValue) {
                $foundginePayload = $payloadValue
            }
        }
    }

    # --------------------------------------------------------
    # Fallback: search JSON recursively for recognizable
    # summary objects when implementation names are not at
    # the top level.
    # --------------------------------------------------------

    if ($null -eq $efCoreCalls -or
        $null -eq $foundgineCalls -or
        $null -eq $efCoreOperations -or
        $null -eq $foundgineOperations) {

        foreach ($file in $jsonFiles) {

            try {
                $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
            }
            catch {
                continue
            }

            $text = $json | ConvertTo-Json -Depth 20

            if ($text -match '"EF Core".*"McpCalls"\s*:\s*(\d+)' -or
                $text -match '"EF Core".*"ToolCalls"\s*:\s*(\d+)') {

                if ($null -eq $efCoreCalls) {
                    $efCoreCalls = [double]$Matches[1]
                }
            }

            if ($text -match '"Foundgine".*"McpCalls"\s*:\s*(\d+)' -or
                $text -match '"Foundgine".*"ToolCalls"\s*:\s*(\d+)') {

                if ($null -eq $foundgineCalls) {
                    $foundgineCalls = [double]$Matches[1]
                }
            }

            if ($text -match '"EF Core".*"LogicalOperations"\s*:\s*(\d+)' -or
                $text -match '"EF Core".*"Operations"\s*:\s*(\d+)') {

                if ($null -eq $efCoreOperations) {
                    $efCoreOperations = [double]$Matches[1]
                }
            }

            if ($text -match '"Foundgine".*"LogicalOperations"\s*:\s*(\d+)' -or
                $text -match '"Foundgine".*"Operations"\s*:\s*(\d+)') {

                if ($null -eq $foundgineOperations) {
                    $foundgineOperations = [double]$Matches[1]
                }
            }
        }
    }

    # --------------------------------------------------------
    # Print observed values
    # --------------------------------------------------------

    Write-Host "Observed benchmark values:" -ForegroundColor White
    Write-Host ""

    if ($null -ne $efCoreCalls) {
        Write-Host ("  EF Core MCP calls:       {0:N2}" -f $efCoreCalls)
    }
    else {
        Write-Host "  EF Core MCP calls:       <not found>" -ForegroundColor DarkYellow
    }

    if ($null -ne $foundgineCalls) {
        Write-Host ("  Foundgine MCP calls:     {0:N2}" -f $foundgineCalls)
    }
    else {
        Write-Host "  Foundgine MCP calls:     <not found>" -ForegroundColor DarkYellow
    }

    if ($null -ne $efCoreOperations) {
        Write-Host ("  EF Core logical ops:     {0:N2}" -f $efCoreOperations)
    }
    else {
        Write-Host "  EF Core logical ops:     <not found>" -ForegroundColor DarkYellow
    }

    if ($null -ne $foundgineOperations) {
        Write-Host ("  Foundgine logical ops:   {0:N2}" -f $foundgineOperations)
    }
    else {
        Write-Host "  Foundgine logical ops:   <not found>" -ForegroundColor DarkYellow
    }

    Write-Host ""

    # --------------------------------------------------------
    # Validate required measurements
    # --------------------------------------------------------

    $missingRequired = @()

    if ($null -eq $efCoreCalls) {
        $missingRequired += "EF Core MCP calls"
    }

    if ($null -eq $foundgineCalls) {
        $missingRequired += "Foundgine MCP calls"
    }

    if ($null -eq $efCoreOperations) {
        $missingRequired += "EF Core logical operations"
    }

    if ($null -eq $foundgineOperations) {
        $missingRequired += "Foundgine logical operations"
    }

    if ($missingRequired.Count -gt 0) {

        Write-Host "Guard rail could not be evaluated." -ForegroundColor Red
        Write-Host ""
        Write-Host "Missing required measurements:" -ForegroundColor Red

        foreach ($item in $missingRequired) {
            Write-Host "  - $item" -ForegroundColor Red
        }

        throw "Benchmark guard rail FAILED because required measurements were unavailable."
    }

    # --------------------------------------------------------
    # Logical operation equivalence
    #
    # Same task means same logical work.
    # --------------------------------------------------------

    $logicalOperationsMatch =
        ($efCoreOperations -eq $foundgineOperations)

    # --------------------------------------------------------
    # MCP call reduction
    # --------------------------------------------------------

    $callReduction = 0

    if ($efCoreCalls -gt 0) {
        $callReduction =
            (($efCoreCalls - $foundgineCalls) / $efCoreCalls) * 100
    }

    $fewerCalls =
        ($foundgineCalls -lt $efCoreCalls)

    # --------------------------------------------------------
    # Payload diagnostics ONLY
    #
    # These intentionally DO NOT participate in the guard rail.
    # --------------------------------------------------------

    $payloadReduction = $null

    if ($null -ne $efCorePayload -and
        $null -ne $foundginePayload -and
        $efCorePayload -ne 0) {

        $payloadReduction =
            (($efCorePayload - $foundginePayload) / $efCorePayload) * 100
    }

    # --------------------------------------------------------
    # Print comparison
    # --------------------------------------------------------

    Write-Host "Run5SameClient comparison:" -ForegroundColor Cyan
    Write-Host ""

    Write-Host ("Tool/MCP calls per task: EF Core={0:N2}; Foundgine={1:N2}" -f `
        $efCoreCalls,
        $foundgineCalls)

    Write-Host ("Logical operations per task: EF Core={0:N2}; Foundgine={1:N2}" -f `
        $efCoreOperations,
        $foundgineOperations)

    Write-Host ("Call reduction: {0:N1}%" -f $callReduction)

    Write-Host ""

    if ($null -ne $efCorePayload -and
        $null -ne $foundginePayload) {

        $averageEfCorePayload = $efCorePayload / $efCoreCalls
        $averageFoundginePayload = $foundginePayload / $foundgineCalls

        $averagePayloadDelta = 0

        if ($averageEfCorePayload -ne 0) {
            $averagePayloadDelta =
                (($averageFoundginePayload - $averageEfCorePayload) /
                    $averageEfCorePayload) * 100
        }

        Write-Host ("Average MCP payload: EF Core={0:N0} bytes; Foundgine={1:N0} bytes" -f `
            $averageEfCorePayload,
            $averageFoundginePayload)

        Write-Host ("Average MCP payload delta: {0:N1}%" -f `
            $averagePayloadDelta)

        Write-Host ("Total task payload: EF Core={0:N0} bytes; Foundgine={1:N0} bytes" -f `
            $efCorePayload,
            $foundginePayload)

        Write-Host ("Total task payload reduction: {0:N1}%" -f `
            $payloadReduction)

        Write-Host ""
        Write-Host "NOTE: Payload size is diagnostic only and does not determine guard-rail success." -ForegroundColor DarkYellow
        Write-Host "      Foundgine intentionally trades protocol calls for richer semantic requests." -ForegroundColor DarkYellow
        Write-Host ""
    }

    # --------------------------------------------------------
    # HARD GUARD RAIL
    # --------------------------------------------------------

    $guardRailPassed =
        $logicalOperationsMatch -and
        $fewerCalls

    Write-Host "Guard-rail checks:" -ForegroundColor Cyan
    Write-Host ""

    if ($logicalOperationsMatch) {
        Write-Host "  PASS  Logical operation equivalence" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL  Logical operation equivalence" -ForegroundColor Red
    }

    if ($fewerCalls) {
        Write-Host "  PASS  Foundgine uses fewer MCP calls" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL  Foundgine uses fewer MCP calls" -ForegroundColor Red
    }

    Write-Host ""

    if (-not $guardRailPassed) {
        throw "Benchmark guard rail FAILED. Full performance matrix was not started."
    }

    Write-Host "[GuardRail-Run5SameClient] completed successfully." -ForegroundColor Green
    Write-Host ""

    return $true
}

# ------------------------------------------------------------
# Locate Run5SameClient guard-rail output
# ------------------------------------------------------------

$Run5SameClientRoot = Join-Path $ScriptRoot "Run5SameClient"

$Run5SameClientResultsCandidates = @(
    (Join-Path $Run5SameClientRoot "results"),
    (Join-Path $Run5SameClientRoot "Results"),
    (Join-Path $Run5SameClientRoot "output"),
    (Join-Path $Run5SameClientRoot "Output"),
    (Join-Path $Run5SameClientRoot "artifacts"),
    (Join-Path $Run5SameClientRoot "Artifacts")
)

$Run5SameClientResults = $Run5SameClientResultsCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if ($null -eq $Run5SameClientResults) {
    Write-Host "Run5SameClient results directory was not found using standard locations." -ForegroundColor DarkYellow
    Write-Host "Searching recursively..." -ForegroundColor Gray

    $candidate = Get-ChildItem `
        -Path $Run5SameClientRoot `
        -Directory `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "results|output|artifacts"
        } |
        Select-Object -First 1

    if ($null -ne $candidate) {
        $Run5SameClientResults = $candidate.FullName
    }
}

# ------------------------------------------------------------
# Run5SameClient
#
# Keep the existing benchmark invocation here.
# ------------------------------------------------------------

$Run5SameClientScript = Join-Path $Run5SameClientRoot "run-run5-same-client.ps1"

if (-not (Test-Path $Run5SameClientScript)) {

    $Run5SameClientScript = Get-ChildItem `
        -Path $Run5SameClientRoot `
        -Filter "*.ps1" `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "run.*5|same.*client"
        } |
        Select-Object -First 1 |
        ForEach-Object { $_.FullName }
}

if ($null -eq $Run5SameClientScript -or
    -not (Test-Path $Run5SameClientScript)) {

    throw "Could not locate the Run5SameClient benchmark script."
}

Invoke-BenchmarkScript -Path $Run5SameClientScript

# ------------------------------------------------------------
# Re-discover results after benchmark execution
# ------------------------------------------------------------

if ($null -eq $Run5SameClientResults) {

    $Run5SameClientResults = $Run5SameClientResultsCandidates |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
}

if ($null -eq $Run5SameClientResults) {

    $candidate = Get-ChildItem `
        -Path $Run5SameClientRoot `
        -Directory `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "results|output|artifacts"
        } |
        Select-Object -First 1

    if ($null -ne $candidate) {
        $Run5SameClientResults = $candidate.FullName
    }
}

if ($null -eq $Run5SameClientResults) {
    throw "Run5SameClient completed but no results directory could be located."
}

# ------------------------------------------------------------
# GUARD RAIL
# ------------------------------------------------------------

Test-Run5SameClientGuardRail `
    -ResultsDirectory $Run5SameClientResults

# ------------------------------------------------------------
# Full performance matrix
#
# This section is reached ONLY when the guard rail passes.
# ------------------------------------------------------------

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Guard rail passed." -ForegroundColor Green
Write-Host " Starting full performance matrix." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# ------------------------------------------------------------
# IMPORTANT:
# The remainder of this file should contain your existing
# full-matrix invocation.
#
# If your existing script already has the full matrix below
# this point, keep that section unchanged.
# ------------------------------------------------------------

$FullMatrixCandidates = @(
    (Join-Path $ScriptRoot "run-agent-end-to-end-performance.ps1"),
    (Join-Path $ScriptRoot "run-performance-matrix.ps1"),
    (Join-Path $ScriptRoot "run-full-performance-matrix.ps1")
)

$FullMatrixScript = $FullMatrixCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if ($null -ne $FullMatrixScript) {

    Invoke-BenchmarkScript -Path $FullMatrixScript

}
else {

    Write-Host "No separate full performance matrix script was found." -ForegroundColor DarkYellow
    Write-Host "If the original run-all-agent-benchmarks.ps1 contains the matrix inline," -ForegroundColor DarkYellow
    Write-Host "restore that existing matrix section after the guard rail." -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Agent benchmark suite completed." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green