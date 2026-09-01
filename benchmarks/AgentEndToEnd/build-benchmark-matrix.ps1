[CmdletBinding()]
param(
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$BenchmarkRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $BenchmarkRoot '..\\..')).Path
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $RepoRoot 'docs-site\\assets\\agent-benchmark\\benchmark-matrix.json'
}

# The matrix is a view over every completed benchmark family.  The old version
# was hard-wired to Run 4, which meant Run 5 and Run 5SameClient could be
# successfully published but could never appear in benchmark-matrix.json.
$runDefinitions = @(
    [pscustomobject]@{ Name = 'Run1'; Root = (Join-Path $BenchmarkRoot 'Run1\\artifacts'); Metadata = 'run4-metadata.json' },
    [pscustomobject]@{ Name = 'Run2'; Root = (Join-Path $BenchmarkRoot 'Run2\\artifacts'); Metadata = 'run4-metadata.json' },
    [pscustomobject]@{ Name = 'Run3'; Root = (Join-Path $BenchmarkRoot 'Run3\\artifacts'); Metadata = 'run4-metadata.json' },
    [pscustomobject]@{ Name = 'Run4'; Root = (Join-Path $BenchmarkRoot 'Run4\\artifacts'); Metadata = 'run4-metadata.json' },
    [pscustomobject]@{ Name = 'Run5'; Root = (Join-Path $BenchmarkRoot 'Run5\\artifacts'); Metadata = 'run5-metadata.json' },
    [pscustomobject]@{ Name = 'Run5SameClient'; Root = (Join-Path $BenchmarkRoot 'Run5SameClient\\artifacts'); Metadata = 'run5-same-client-metadata.json' }
)

$assumptions = [ordered]@{
    callsPerDay = 100000
    inputUsdPerMillionTokens = 3.0
    energyWhPer1000Tokens = 0.30
    currency = 'USD'
    energyNote = 'Illustrative estimate from heuristic context tokens; not measured power draw.'
    costNote = 'Annual price assumes 100,000 benchmark flows/day at $3 per million estimated context tokens.'
}

function Get-Number($value, [double]$default = 0) {
    if ($null -eq $value) { return $default }
    return [double]$value
}

function Add-Run4Samples([string]$runName, $doc, [System.Collections.Generic.List[object]]$target) {
    foreach ($sample in @($doc.samples)) {
        $target.Add([pscustomobject]@{
            run = [int]$sample.run
            runFamily = $runName
            variant = 'standard'
            batch = [int]$sample.customerCount
            customers = [int]$sample.customerCount
            concurrency = [int]$sample.concurrency
            implementation = [string]$sample.implementation
            option = [string]$sample.option
            rps = Get-Number $sample.rps
            avgWallMs = Get-Number $sample.avgWallMs
            p50Ms = Get-Number $sample.p50Ms
            p95Ms = Get-Number $sample.p95Ms
            p99Ms = Get-Number $sample.p99Ms
            maxWallMs = Get-Number $sample.maxWallMs
            success = [int](Get-Number $sample.success)
            failed = [int](Get-Number $sample.failed)
            toolCalls = Get-Number $sample.toolCalls
            logicalOps = if ($null -ne $sample.logicalOps) { Get-Number $sample.logicalOps } else { 0 }
            estimatedInputTokens = Get-Number $sample.estimatedInputTokens
            estimatedOutputTokens = Get-Number $sample.estimatedOutputTokens
            estimatedContextTokens = if ($null -ne $sample.estimatedContextTokens) { Get-Number $sample.estimatedContextTokens } else { (Get-Number $sample.estimatedInputTokens) + (Get-Number $sample.estimatedOutputTokens) }
        })
    }
}

function Get-Percentile([double[]]$values, [double]$p) {
    if ($values.Count -eq 0) { return 0 }
    $sorted = @($values | Sort-Object)
    if ($sorted.Count -eq 1) { return $sorted[0] }
    $position = ($sorted.Count - 1) * $p
    $lower = [math]::Floor($position)
    $upper = [math]::Ceiling($position)
    if ($lower -eq $upper) { return $sorted[$lower] }
    return $sorted[$lower] + (($sorted[$upper] - $sorted[$lower]) * ($position - $lower))
}

function Add-SameClientSamples([string]$runName, $doc, [System.Collections.Generic.List[object]]$target) {
    # Run 5 Same Client persists task samples, not RunSummary objects. The
    # runner appends exactly `concurrency` samples per measured iteration, in
    # order, for each implementation. Reconstruct each iteration so RPS uses
    # the slowest concurrent worker, exactly as Runner/Program.cs does.
    $concurrency = [int]$doc.concurrency
    foreach ($implGroup in (@($doc.samples) | Group-Object implementation)) {
        $samples = @($implGroup.Group)
        if (($samples.Count % $concurrency) -ne 0) {
            throw "Run5SameClient sample count $($samples.Count) is not divisible by concurrency $concurrency."
        }

        for ($offset = 0; $offset -lt $samples.Count; $offset += $concurrency) {
            $iteration = @($samples[$offset..($offset + $concurrency - 1)])
            $ok = @($iteration | Where-Object { [bool]$_.Success })
            $maxWall = if ($ok.Count -gt 0) { ($ok | Measure-Object -Property WallMs -Maximum).Maximum } else { 0 }
            $iterationLogicalOps = if ($ok.Count -gt 0) { ($ok | Measure-Object -Property LogicalOps -Sum).Sum } else { 0 }
            $iterationRps = if ($maxWall -gt 0) { [double]$iterationLogicalOps * 1000.0 / [double]$maxWall } else { 0 }
            $runNumber = [int]($offset / $concurrency) + 1

            foreach ($sample in $iteration) {
                $logicalOps = Get-Number $sample.LogicalOps
                $wall = Get-Number $sample.WallMs
                $inputBytes = Get-Number $sample.InputBytes
                $outputBytes = Get-Number $sample.OutputBytes
                # Token heuristic is bytes / 4. Store PER TOOL CALL here;
                # the website normalizes once to per logical operation using
                # logicalOps/toolCalls. This avoids double-normalizing batches.
                $toolCalls = Get-Number $sample.ToolCalls
                $callDivisor = if ($toolCalls -gt 0) { $toolCalls } else { 1 }
                $inputTokens = ($inputBytes / 4.0) / $callDivisor
                $outputTokens = ($outputBytes / 4.0) / $callDivisor
                $contextTokens = $inputTokens + $outputTokens
                $success = if ([bool]$sample.Success) { 1 } else { 0 }

                $target.Add([pscustomobject]@{
                    run = $runNumber
                    runFamily = $runName
                    variant = 'same-client'
                    batch = [int]$doc.customers
                    customers = [int]$doc.customers
                    concurrency = $concurrency
                    implementation = [string]$sample.Implementation
                    option = $runName
                    # Assign the reconstructed iteration throughput to each
                    # task in that fixed-size iteration. Averaging later is
                    # therefore exactly the mean iteration RPS.
                    rps = $iterationRps
                    avgWallMs = $wall
                    p50Ms = $wall
                    p95Ms = $wall
                    p99Ms = $wall
                    maxWallMs = $wall
                    success = $success
                    failed = 1 - $success
                    toolCalls = $toolCalls
                    logicalOps = $logicalOps
                    estimatedInputTokens = $inputTokens
                    estimatedOutputTokens = $outputTokens
                    estimatedContextTokens = $contextTokens
                })
            }
        }
    }
}

$raw = [System.Collections.Generic.List[object]]::new()
$includedRuns = [System.Collections.Generic.List[string]]::new()

foreach ($definition in $runDefinitions) {
    if (-not (Test-Path -LiteralPath $definition.Root -PathType Container)) {
        continue
    }

    $files = @(Get-ChildItem -LiteralPath $definition.Root -Recurse -Filter $definition.Metadata -File)
    if ($files.Count -eq 0) {
        continue
    }

    $includedRuns.Add($definition.Name)
    foreach ($file in $files) {
        $doc = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json

        # Exclude the one-off 1-customer/1-concurrency smoke fixture from the
        # published Run 5 Same Client matrix.  The production matrix is the
        # same 4x4 customer/concurrency grid used by Run 5.
        if ($definition.Name -eq 'Run5SameClient' -and
            (([int]$doc.customers) -notin @(10,100,1000,10000) -or ([int]$doc.concurrency) -notin @(8,16,32,64))) {
            continue
        }

        if ($definition.Name -eq 'Run5SameClient') {
            Add-SameClientSamples $definition.Name $doc $raw
        }
        else {
            Add-Run4Samples $definition.Name $doc $raw
        }
    }
}

if ($raw.Count -eq 0) {
    throw "No benchmark metadata found under: $BenchmarkRoot"
}

$aggregate = [System.Collections.Generic.List[object]]::new()
foreach ($group in ($raw | Group-Object runFamily,variant,batch,concurrency,implementation)) {
    $items = @($group.Group)
    $first = $items[0]
    $avg = { param($name) (($items | Measure-Object -Property $name -Average).Average) }
    $ctx = & $avg 'estimatedContextTokens'
    $rps = & $avg 'rps'

    $aggregate.Add([pscustomobject]@{
        run = $first.run
        runFamily = $first.runFamily
        variant = $first.variant
        batch = $first.batch
        customers = $first.customers
        concurrency = $first.concurrency
        implementation = $first.implementation
        option = $first.option
        samples = $items.Count
        avgRps = $rps
        avgWallMs = & $avg 'avgWallMs'
        p50Ms = if ($first.variant -eq 'same-client') { Get-Percentile @($items | ForEach-Object { $_.avgWallMs }) 0.50 } else { & $avg 'p50Ms' }
        p95Ms = if ($first.variant -eq 'same-client') { Get-Percentile @($items | ForEach-Object { $_.avgWallMs }) 0.95 } else { & $avg 'p95Ms' }
        p99Ms = if ($first.variant -eq 'same-client') { Get-Percentile @($items | ForEach-Object { $_.avgWallMs }) 0.99 } else { & $avg 'p99Ms' }
        maxWallMs = ($items | Measure-Object -Property maxWallMs -Maximum).Maximum
        success = (($items | Measure-Object -Property success -Sum).Sum)
        failed = (($items | Measure-Object -Property failed -Sum).Sum)
        toolCalls = & $avg 'toolCalls'
        logicalOps = & $avg 'logicalOps'
        estimatedInputTokens = & $avg 'estimatedInputTokens'
        estimatedOutputTokens = & $avg 'estimatedOutputTokens'
        estimatedContextTokens = $ctx
        annualCostUsd = ($ctx / 1000000.0) * $assumptions.callsPerDay * 365 * $assumptions.inputUsdPerMillionTokens
        annualEnergyKwh = ($ctx / 1000.0) * $assumptions.callsPerDay * 365 * $assumptions.energyWhPer1000Tokens / 1000.0
        efficiencyRpsPer1kTokens = if ($ctx -gt 0) { $rps / ($ctx / 1000.0) } else { 0 }
    })
}

$dir = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$result = [ordered]@{
    schemaVersion = 3
    generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
    source = 'AgentEndToEnd benchmark families'
    runs = @($includedRuns)
    warmups = 5
    assumptions = $assumptions
    aggregate = @($aggregate | Sort-Object run,variant,batch,concurrency,implementation)
    samples = @($raw | Sort-Object run,variant,batch,concurrency,implementation)
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Destination -Encoding UTF8
Write-Host "Built benchmark matrix: $Destination" -ForegroundColor Green
Write-Host "  Runs:           $($includedRuns -join ', ')"
Write-Host "  Aggregate rows: $($aggregate.Count)"
Write-Host "  Sample rows:    $($raw.Count)"
