[CmdletBinding()]
param(
    [string]$Run4Root,
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$BenchmarkRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $BenchmarkRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Run4Root)) { $Run4Root = Join-Path $BenchmarkRoot 'Run4\artifacts' }
if ([string]::IsNullOrWhiteSpace($Destination)) { $Destination = Join-Path $RepoRoot 'docs-site\assets\agent-benchmark\benchmark-matrix.json' }

if (-not (Test-Path -LiteralPath $Run4Root -PathType Container)) { throw "Run 4 artifact directory not found: $Run4Root" }

$summaryFiles = @(Get-ChildItem -LiteralPath $Run4Root -Recurse -Filter 'run4-metadata.json' -File)
if ($summaryFiles.Count -eq 0) { throw "No run4-metadata.json files found under: $Run4Root" }

$assumptions = [ordered]@{
    callsPerDay = 100000
    inputUsdPerMillionTokens = 3.0
    energyWhPer1000Tokens = 0.30
    currency = 'USD'
    energyNote = 'Illustrative estimate from heuristic context tokens; not measured power draw.'
    costNote = 'Annual price assumes 100,000 benchmark flows/day at $3 per million estimated context tokens.'
}

$raw = @()
foreach ($file in $summaryFiles) {
    $doc = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    foreach ($sample in @($doc.samples)) {
        $raw += [pscustomobject]@{
            run = [int]$sample.run
            batch = [int]$sample.customerCount
            customers = [int]$sample.customerCount
            concurrency = [int]$sample.concurrency
            implementation = [string]$sample.implementation
            option = [string]$sample.option
            rps = [double]$sample.rps
            avgWallMs = [double]$sample.avgWallMs
            p50Ms = [double]$sample.p50Ms
            p95Ms = [double]$sample.p95Ms
            p99Ms = [double]$sample.p99Ms
            maxWallMs = [double]$sample.maxWallMs
            success = [int]$sample.success
            failed = [int]$sample.failed
            toolCalls = [int]$sample.toolCalls
            estimatedInputTokens = [double]$sample.estimatedInputTokens
            estimatedOutputTokens = [double]$sample.estimatedOutputTokens
            estimatedContextTokens = [double]$sample.estimatedContextTokens
        }
    }
}

$aggregate = @()
foreach ($group in ($raw | Group-Object batch,concurrency,implementation)) {
    $items = @($group.Group)
    $first = $items[0]
    $avg = { param($name) (($items | Measure-Object -Property $name -Average).Average) }
    $ctx = & $avg 'estimatedContextTokens'
    $rps = & $avg 'rps'
    $aggregate += [pscustomobject]@{
        batch = $first.batch
        customers = $first.customers
        concurrency = $first.concurrency
        implementation = $first.implementation
        option = $first.option
        samples = $items.Count
        avgRps = $rps
        avgWallMs = & $avg 'avgWallMs'
        p50Ms = & $avg 'p50Ms'
        p95Ms = & $avg 'p95Ms'
        p99Ms = & $avg 'p99Ms'
        success = (($items | Measure-Object -Property success -Sum).Sum)
        failed = (($items | Measure-Object -Property failed -Sum).Sum)
        toolCalls = & $avg 'toolCalls'
        estimatedInputTokens = & $avg 'estimatedInputTokens'
        estimatedOutputTokens = & $avg 'estimatedOutputTokens'
        estimatedContextTokens = $ctx
        annualCostUsd = ($ctx / 1000000.0) * $assumptions.callsPerDay * 365 * $assumptions.inputUsdPerMillionTokens
        annualEnergyKwh = ($ctx / 1000.0) * $assumptions.callsPerDay * 365 * $assumptions.energyWhPer1000Tokens / 1000.0
        efficiencyRpsPer1kTokens = if ($ctx -gt 0) { $rps / ($ctx / 1000.0) } else { 0 }
    }
}

$dir = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$result = [ordered]@{
    schemaVersion = 2
    generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
    source = 'Run 4 agent benchmark'
    runs = if ($raw.Count) { ($raw | Measure-Object -Property run -Maximum).Maximum } else { 0 }
    warmups = 5
    assumptions = $assumptions
    aggregate = @($aggregate | Sort-Object batch,concurrency,implementation)
    samples = @($raw | Sort-Object batch,concurrency,implementation,run)
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Destination -Encoding UTF8
Write-Host "Built benchmark matrix: $Destination" -ForegroundColor Green
Write-Host "  Aggregate rows: $($aggregate.Count)"
Write-Host "  Sample rows:    $($raw.Count)"
