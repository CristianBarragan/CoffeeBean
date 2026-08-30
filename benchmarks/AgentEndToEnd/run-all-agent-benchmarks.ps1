$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptRoot "..\..")).Path

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Foundgine Agent End-to-End Benchmark Suite" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Script root : $ScriptRoot" -ForegroundColor Gray
Write-Host "Repo root   : $RepoRoot" -ForegroundColor Gray
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

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Benchmark script was not found: $Path"
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $scriptDirectory = Split-Path -Parent $resolvedPath

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host " Running: $resolvedPath" -ForegroundColor DarkCyan
    Write-Host " Working directory: $scriptDirectory" -ForegroundColor DarkCyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host ""

    Push-Location $scriptDirectory

    try {
        & powershell.exe `
            -NoLogo `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $resolvedPath `
            @Arguments

        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "Benchmark script failed: $resolvedPath (exit code $exitCode)"
    }

    Write-Host ""
    Write-Host "Completed successfully: $resolvedPath" -ForegroundColor Green
    Write-Host ""
}

function Get-JsonPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        $Object,

        [Parameter(Mandatory = $true)]
        [string[]] $Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {

        $property = $Object.PSObject.Properties |
            Where-Object {
                $_.Name -ieq $name
            } |
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

function Find-Run5ResultsDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Run5Root
    )

    $standardCandidates = @(
        (Join-Path $Run5Root "results"),
        (Join-Path $Run5Root "Results"),
        (Join-Path $Run5Root "output"),
        (Join-Path $Run5Root "Output"),
        (Join-Path $Run5Root "artifacts"),
        (Join-Path $Run5Root "Artifacts")
    )

    foreach ($candidate in $standardCandidates) {
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $candidate = Get-ChildItem `
        -LiteralPath $Run5Root `
        -Directory `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "^(results|output|artifacts)$"
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -ne $candidate) {
        return $candidate.FullName
    }

    return $null
}

# ------------------------------------------------------------
# Run5SameClient guard rail
#
# HARD REQUIREMENTS:
#
#   1. EF Core completed
#   2. Foundgine completed
#   3. Logical operation count is equivalent
#   4. Foundgine uses fewer MCP calls
#
# Payload is diagnostic only.
# ------------------------------------------------------------

function Test-Run5SameClientGuardRail {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResultsDirectory
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host " [GuardRail-Run5SameClient]" -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path -LiteralPath $ResultsDirectory -PathType Container)) {
        throw "Run5SameClient results directory was not found: $ResultsDirectory"
    }

    $resolvedResults = (Resolve-Path -LiteralPath $ResultsDirectory).Path

    Write-Host "Results directory:" -ForegroundColor Gray
    Write-Host "  $resolvedResults" -ForegroundColor Gray
    Write-Host ""

    # --------------------------------------------------------
    # Only inspect JSON files containing Run5 benchmark data.
    # Do not accidentally consume unrelated JSON files.
    # --------------------------------------------------------

    $jsonFiles = @(
        Get-ChildItem `
            -LiteralPath $resolvedResults `
            -Filter "*.json" `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    )

    if ($jsonFiles.Count -eq 0) {
        throw "No Run5SameClient JSON result files were found in: $resolvedResults"
    }

    Write-Host "Found $($jsonFiles.Count) JSON result file(s)." -ForegroundColor Gray
    Write-Host ""

    $records = @()

    foreach ($file in $jsonFiles) {

        try {
            $jsonText = Get-Content `
                -LiteralPath $file.FullName `
                -Raw `
                -ErrorAction Stop

            $json = $jsonText | ConvertFrom-Json -ErrorAction Stop
        }
        catch {
            Write-Host "Skipping invalid JSON: $($file.FullName)" -ForegroundColor DarkYellow
            continue
        }

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
            ""
        }

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
                "totalMcpCalls",
                "TotalToolCalls",
                "totalToolCalls"
            )

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
                "operationCount",
                "TotalLogicalOperations",
                "totalLogicalOperations"
            )

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

        if ($null -ne $callsValue -or
            $null -ne $operationsValue -or
            $null -ne $payloadValue) {

            $records += [PSCustomObject]@{
                File           = $file.FullName
                Implementation = $implementationText
                Calls          = $callsValue
                Operations     = $operationsValue
                Payload        = $payloadValue
            }
        }
    }

    if ($records.Count -eq 0) {
        throw "JSON files were found, but none contained recognizable Run5 benchmark measurements."
    }

    Write-Host "Recognized benchmark records:" -ForegroundColor Gray
    Write-Host ""

    foreach ($record in $records) {

        Write-Host ("  {0}" -f $record.File) -ForegroundColor DarkGray

        if ($record.Implementation) {
            Write-Host ("    Implementation : {0}" -f $record.Implementation)
        }

        if ($null -ne $record.Calls) {
            Write-Host ("    MCP calls      : {0:N2}" -f $record.Calls)
        }

        if ($null -ne $record.Operations) {
            Write-Host ("    Logical ops    : {0:N2}" -f $record.Operations)
        }

        if ($null -ne $record.Payload) {
            Write-Host ("    Payload        : {0:N0} bytes" -f $record.Payload)
        }

        Write-Host ""
    }

    # --------------------------------------------------------
    # Identify EF Core and Foundgine.
    # --------------------------------------------------------

    $efRecords = @(
        $records |
        Where-Object {
            $_.Implementation -match "EF\s*Core|EntityFramework|Conventional"
        }
    )

    $foundgineRecords = @(
        $records |
        Where-Object {
            $_.Implementation -match "Foundgine|Semantic"
        }
    )

    # --------------------------------------------------------
    # Fallback: inspect serialized JSON text.
    # --------------------------------------------------------

    if ($efRecords.Count -eq 0 -or $foundgineRecords.Count -eq 0) {

        Write-Host "Implementation metadata was incomplete." -ForegroundColor DarkYellow
        Write-Host "Running fallback recognition..." -ForegroundColor DarkYellow
        Write-Host ""

        foreach ($file in $jsonFiles) {

            try {
                $text = Get-Content `
                    -LiteralPath $file.FullName `
                    -Raw `
                    -ErrorAction Stop
            }
            catch {
                continue
            }

            if ($efRecords.Count -eq 0 -and
                $text -match '(?is)"EF\s*Core".{0,2000}?"(?:McpCalls|ToolCalls|McpToolCalls|TotalMcpCalls)"\s*:\s*(\d+(?:\.\d+)?)') {

                $efRecords += [PSCustomObject]@{
                    File           = $file.FullName
                    Implementation = "EF Core"
                    Calls          = [double]$Matches[1]
                    Operations     = $null
                    Payload        = $null
                }
            }

            if ($foundgineRecords.Count -eq 0 -and
                $text -match '(?is)"Foundgine".{0,2000}?"(?:McpCalls|ToolCalls|McpToolCalls|TotalMcpCalls)"\s*:\s*(\d+(?:\.\d+)?)') {

                $foundgineRecords += [PSCustomObject]@{
                    File           = $file.FullName
                    Implementation = "Foundgine"
                    Calls          = [double]$Matches[1]
                    Operations     = $null
                    Payload        = $null
                }
            }
        }
    }

    # --------------------------------------------------------
    # Select actual records.
    #
    # Prefer records that have ALL required measurements.
    # --------------------------------------------------------

    $efRecord = $efRecords |
        Where-Object {
            $null -ne $_.Calls -and
            $null -ne $_.Operations
        } |
        Sort-Object File |
        Select-Object -First 1

    if ($null -eq $efRecord) {
        $efRecord = $efRecords |
            Where-Object {
                $null -ne $_.Calls
            } |
            Select-Object -First 1
    }

    $foundgineRecord = $foundgineRecords |
        Where-Object {
            $null -ne $_.Calls -and
            $null -ne $_.Operations
        } |
        Sort-Object File |
        Select-Object -First 1

    if ($null -eq $foundgineRecord) {
        $foundgineRecord = $foundgineRecords |
            Where-Object {
                $null -ne $_.Calls
            } |
            Select-Object -First 1
    }

    # --------------------------------------------------------
    # Aggregate measurements if the JSON layout stores
    # implementation data separately.
    # --------------------------------------------------------

    $efCoreCalls = $null
    $foundgineCalls = $null

    $efCoreOperations = $null
    $foundgineOperations = $null

    $efCorePayload = $null
    $foundginePayload = $null

    if ($null -ne $efRecord) {
        $efCoreCalls = $efRecord.Calls
        $efCoreOperations = $efRecord.Operations
        $efCorePayload = $efRecord.Payload
    }

    if ($null -ne $foundgineRecord) {
        $foundgineCalls = $foundgineRecord.Calls
        $foundgineOperations = $foundgineRecord.Operations
        $foundginePayload = $foundgineRecord.Payload
    }

    # --------------------------------------------------------
    # Last-resort recursive summary search.
    # --------------------------------------------------------

    foreach ($file in $jsonFiles) {

        if ($null -ne $efCoreCalls -and
            $null -ne $efCoreOperations -and
            $null -ne $foundgineCalls -and
            $null -ne $foundgineOperations) {
            break
        }

        try {
            $text = Get-Content `
                -LiteralPath $file.FullName `
                -Raw `
                -ErrorAction Stop
        }
        catch {
            continue
        }

        if ($null -eq $efCoreCalls) {
            if ($text -match '(?is)"EF\s*Core".{0,5000}?"(?:McpCalls|ToolCalls|McpToolCalls|TotalMcpCalls)"\s*:\s*(\d+(?:\.\d+)?)') {
                $efCoreCalls = [double]$Matches[1]
            }
        }

        if ($null -eq $foundgineCalls) {
            if ($text -match '(?is)"Foundgine".{0,5000}?"(?:McpCalls|ToolCalls|McpToolCalls|TotalMcpCalls)"\s*:\s*(\d+(?:\.\d+)?)') {
                $foundgineCalls = [double]$Matches[1]
            }
        }

        if ($null -eq $efCoreOperations) {
            if ($text -match '(?is)"EF\s*Core".{0,5000}?"(?:LogicalOperations|LogicalOperationCount|Operations|OperationCount)"\s*:\s*(\d+(?:\.\d+)?)') {
                $efCoreOperations = [double]$Matches[1]
            }
        }

        if ($null -eq $foundgineOperations) {
            if ($text -match '(?is)"Foundgine".{0,5000}?"(?:LogicalOperations|LogicalOperationCount|Operations|OperationCount)"\s*:\s*(\d+(?:\.\d+)?)') {
                $foundgineOperations = [double]$Matches[1]
            }
        }

        if ($null -eq $efCorePayload) {
            if ($text -match '(?is)"EF\s*Core".{0,5000}?"(?:TotalTaskPayload|TaskPayloadBytes|TotalPayloadBytes|PayloadBytes)"\s*:\s*(\d+(?:\.\d+)?)') {
                $efCorePayload = [double]$Matches[1]
            }
        }

        if ($null -eq $foundginePayload) {
            if ($text -match '(?is)"Foundgine".{0,5000}?"(?:TotalTaskPayload|TaskPayloadBytes|TotalPayloadBytes|PayloadBytes)"\s*:\s*(\d+(?:\.\d+)?)') {
                $foundginePayload = [double]$Matches[1]
            }
        }
    }

    # --------------------------------------------------------
    # Print observed values.
    # --------------------------------------------------------

    Write-Host ""
    Write-Host "Observed benchmark values:" -ForegroundColor White
    Write-Host ""

    if ($null -ne $efCoreCalls) {
        Write-Host ("  EF Core MCP calls:       {0:N2}" -f $efCoreCalls)
    }
    else {
        Write-Host "  EF Core MCP calls:       <not found>" -ForegroundColor Red
    }

    if ($null -ne $foundgineCalls) {
        Write-Host ("  Foundgine MCP calls:     {0:N2}" -f $foundgineCalls)
    }
    else {
        Write-Host "  Foundgine MCP calls:     <not found>" -ForegroundColor Red
    }

    if ($null -ne $efCoreOperations) {
        Write-Host ("  EF Core logical ops:     {0:N2}" -f $efCoreOperations)
    }
    else {
        Write-Host "  EF Core logical ops:     <not found>" -ForegroundColor Red
    }

    if ($null -ne $foundgineOperations) {
        Write-Host ("  Foundgine logical ops:   {0:N2}" -f $foundgineOperations)
    }
    else {
        Write-Host "  Foundgine logical ops:   <not found>" -ForegroundColor Red
    }

    Write-Host ""

    # --------------------------------------------------------
    # Required measurements.
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

        foreach ($item in $missingRequired) {
            Write-Host "  MISSING: $item" -ForegroundColor Red
        }

        Write-Host ""

        throw "Benchmark guard rail FAILED because required measurements were unavailable."
    }

    # --------------------------------------------------------
    # Logical equivalence.
    # --------------------------------------------------------

    $logicalOperationsMatch =
        ($efCoreOperations -eq $foundgineOperations)

    # --------------------------------------------------------
    # Call reduction.
    # --------------------------------------------------------

    $callReduction = 0

    if ($efCoreCalls -gt 0) {
        $callReduction =
            (($efCoreCalls - $foundgineCalls) / $efCoreCalls) * 100
    }

    $fewerCalls =
        ($foundgineCalls -lt $efCoreCalls)

    # --------------------------------------------------------
    # Payload diagnostics.
    # --------------------------------------------------------

    $payloadReduction = $null

    if ($null -ne $efCorePayload -and
        $null -ne $foundginePayload -and
        $efCorePayload -ne 0) {

        $payloadReduction =
            (($efCorePayload - $foundginePayload) /
                $efCorePayload) * 100
    }

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

        if ($efCoreCalls -gt 0) {
            $averageEfCorePayload =
                $efCorePayload / $efCoreCalls
        }
        else {
            $averageEfCorePayload = 0
        }

        if ($foundgineCalls -gt 0) {
            $averageFoundginePayload =
                $foundginePayload / $foundgineCalls
        }
        else {
            $averageFoundginePayload = 0
        }

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
        Write-Host "NOTE: Payload size is diagnostic only." -ForegroundColor DarkYellow
        Write-Host "      It does NOT determine guard-rail success." -ForegroundColor DarkYellow
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

# ============================================================
# RUN5
# ============================================================

$Run5SameClientRoot =
    Join-Path $ScriptRoot "Run5SameClient"

if (-not (Test-Path -LiteralPath $Run5SameClientRoot -PathType Container)) {
    throw "Run5SameClient directory was not found: $Run5SameClientRoot"
}

$Run5SameClientRoot =
    (Resolve-Path -LiteralPath $Run5SameClientRoot).Path

Write-Host "Run5SameClient root:" -ForegroundColor Gray
Write-Host "  $Run5SameClientRoot" -ForegroundColor Gray
Write-Host ""

# ------------------------------------------------------------
# Locate benchmark script.
# ------------------------------------------------------------

$Run5SameClientScriptCandidates = @(
    (Join-Path $Run5SameClientRoot "run-run5-same-client.ps1"),
    (Join-Path $Run5SameClientRoot "run5-same-client.ps1"),
    (Join-Path $Run5SameClientRoot "run-run5.ps1")
)

$Run5SameClientScript = $Run5SameClientScriptCandidates |
    Where-Object {
        Test-Path -LiteralPath $_ -PathType Leaf
    } |
    Select-Object -First 1

if ($null -eq $Run5SameClientScript) {

    $Run5SameClientScript = Get-ChildItem `
        -LiteralPath $Run5SameClientRoot `
        -Filter "*.ps1" `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "run.*5|same.*client"
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 |
        ForEach-Object {
            $_.FullName
        }
}

if ($null -eq $Run5SameClientScript) {
    throw "Could not locate the Run5SameClient benchmark script."
}

Write-Host "Run5SameClient script:" -ForegroundColor Gray
Write-Host "  $Run5SameClientScript" -ForegroundColor Gray
Write-Host ""

# ------------------------------------------------------------
# IMPORTANT:
#
# Do NOT resolve results before executing Run5.
# Existing results may be stale.
# ------------------------------------------------------------

$beforeRun = Get-Date

Invoke-BenchmarkScript `
    -Path $Run5SameClientScript

# ------------------------------------------------------------
# Discover results AFTER benchmark execution.
# ------------------------------------------------------------

$Run5SameClientResults =
    Find-Run5ResultsDirectory `
        -Run5Root $Run5SameClientRoot

if ($null -eq $Run5SameClientResults) {
    throw "Run5SameClient completed but no results directory could be located."
}

Write-Host "Run5SameClient results:" -ForegroundColor Gray
Write-Host "  $Run5SameClientResults" -ForegroundColor Gray
Write-Host ""

# ------------------------------------------------------------
# Guard rail
# ------------------------------------------------------------

Test-Run5SameClientGuardRail `
    -ResultsDirectory $Run5SameClientResults

# ============================================================
# FULL PERFORMANCE MATRIX
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Guard rail passed." -ForegroundColor Green
Write-Host " Starting full performance matrix." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

$FullMatrixCandidates = @(
    (Join-Path $ScriptRoot "run-agent-end-to-end-performance.ps1"),
    (Join-Path $ScriptRoot "run-performance-matrix.ps1"),
    (Join-Path $ScriptRoot "run-full-performance-matrix.ps1")
)

$FullMatrixScript = $FullMatrixCandidates |
    Where-Object {
        Test-Path -LiteralPath $_ -PathType Leaf
    } |
    Select-Object -First 1

if ($null -eq $FullMatrixScript) {

    $FullMatrixScript = Get-ChildItem `
        -LiteralPath $ScriptRoot `
        -Filter "*.ps1" `
        -File `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "performance.*(matrix|end.to.end)|full.*matrix"
        } |
        Select-Object -First 1 |
        ForEach-Object {
            $_.FullName
        }
}

if ($null -ne $FullMatrixScript) {

    Invoke-BenchmarkScript `
        -Path $FullMatrixScript
}
else {

    Write-Host "No separate full performance matrix script was found." -ForegroundColor DarkYellow
    Write-Host "The Run5 guard rail itself passed successfully." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Agent benchmark suite completed." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""