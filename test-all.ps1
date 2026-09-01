[CmdletBinding()]
param(
    [switch]$SkipPostgres,
    [switch]$SkipPentest,
    [switch]$KeepPostgres
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$composeFile = Join-Path $repo 'docker-compose.postgres.yml'
$connectionString = 'Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine'

$results = [ordered]@{
    UNIT = 'SKIP'
    COMPONENT = 'SKIP'
    'EF/POSTGRES' = 'SKIP'
    E2E = 'SKIP'
    SECURITY = 'SKIP'
    PENTEST = 'SKIP'
    AOT = 'SKIP'
    GRAPHQL = 'SKIP'
    MCP = 'SKIP'
}

function Invoke-TestProject {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string[]]$Projects
    )

    Write-Host "`n[$Name]" -ForegroundColor Cyan
    $failed = $false
    foreach ($project in $Projects) {
        $path = Join-Path $repo $project
        Write-Host "  dotnet test $project"
        & dotnet test $path --configuration Release --no-restore --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    }
    $results[$Name] = if ($failed) { 'FAIL' } else { 'PASS' }
    return -not $failed
}

try {
    Write-Host '=== Foundgine repository test gate ===' -ForegroundColor Green
    Write-Host "Repository: $repo"

    & dotnet restore (Join-Path $repo 'Foundgine.sln') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    Invoke-TestProject 'UNIT' @(
        'tests/Foundgine.Semantics.Tests/Foundgine.Semantics.Tests.csproj',
        'tests/Foundgine.Planning.Tests/Foundgine.Planning.Tests.csproj',
        'tests/Foundgine.InMemory.Tests/Foundgine.InMemory.Tests.csproj',
        'tests/Foundgine.Intent.Json.Tests/Foundgine.Intent.Json.Tests.csproj'
    ) | Out-Null

    Invoke-TestProject 'COMPONENT' @(
        'tests/Foundgine.Sql.Tests/Foundgine.Sql.Tests.csproj',
        'tests/Foundgine.HighAssurance.Tests/Foundgine.HighAssurance.Tests.csproj',
        'tests/Foundgine.Postgres.Vector.Tests/Foundgine.Postgres.Vector.Tests.csproj'
    ) | Out-Null

    Invoke-TestProject 'GRAPHQL' @(
        'tests/Foundgine.GraphQL.HotChocolate.Tests/Foundgine.GraphQL.HotChocolate.Tests.csproj'
    ) | Out-Null

    Invoke-TestProject 'MCP' @(
        'tests/Foundgine.MCP.Tests/Foundgine.MCP.Tests.csproj'
    ) | Out-Null

    Invoke-TestProject 'SECURITY' @(
        'tests/Foundgine.Security.Tests/Foundgine.Security.Tests.csproj',
        'tests/Foundgine.Security.Authority.Tests/Foundgine.Security.Authority.Tests.csproj'
    ) | Out-Null

    Invoke-TestProject 'AOT' @(
        'tests/Foundgine.Aot.Tests/Foundgine.Aot.Tests.csproj'
    ) | Out-Null

    if (-not $SkipPostgres) {
        Write-Host "`n[EF/POSTGRES] Starting PostgreSQL 17 + pgvector" -ForegroundColor Cyan
        $dockerAvailable = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
        if ($dockerAvailable) {
            & docker version --format '{{.Server.Version}}' | Out-Null
            $dockerAvailable = ($LASTEXITCODE -eq 0)
        }

        if ($dockerAvailable) {
            & docker compose -f $composeFile up -d postgres
            if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL container failed to start.' }

            $ready = $false
            for ($i = 0; $i -lt 60; $i++) {
                & docker compose -f $composeFile exec -T postgres pg_isready -U foundgine -d foundgine_e2e 2>$null | Out-Null
                if ($LASTEXITCODE -eq 0) { $ready = $true; break }
                Start-Sleep -Seconds 1
            }
            if (-not $ready) { throw 'PostgreSQL did not become ready within 60 seconds.' }

            $env:FOUNDGINE_POSTGRES_CONNECTION_STRING = $connectionString
            $postgresOk = Invoke-TestProject 'EF/POSTGRES' @(
                'tests/Foundgine.Testing/Foundgine.Testing.csproj',
                'tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj'
            )
            $results['E2E'] = if ($postgresOk) { 'PASS' } else { 'FAIL' }
        }
        else {
            Write-Warning 'Docker is unavailable; EF/POSTGRES and database-backed E2E tests are SKIPPED.'
        }
    }

    if (-not $SkipPentest) {
        if ($env:FOUNDGINE_TARGET_HOST -and $env:FOUNDGINE_TARGET_URL) {
            Write-Host "`n[PENTEST] Running authorized live penetration gate" -ForegroundColor Cyan
            & (Join-Path $repo 'security/pentest/run-all.ps1')
            $results.PENTEST = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
        }
        else {
            Write-Warning 'FOUNDGINE_TARGET_HOST/FOUNDGINE_TARGET_URL are not set; live PENTEST gate is SKIPPED.'
        }
    }
}
catch {
    Write-Error $_
}
finally {
    if (-not $KeepPostgres -and -not $SkipPostgres) {
        try { & docker compose -f $composeFile stop postgres | Out-Null } catch { }
    }

    Write-Host "`n=== TEST GATE ===" -ForegroundColor Green
    foreach ($entry in $results.GetEnumerator()) {
        '{0,-16} {1}' -f $entry.Key, $entry.Value | Write-Host
    }

    if ($results.Values -contains 'FAIL') { exit 1 }
}
