param(
    [string]$Configuration = 'Release',
    [string]$Output = 'artifacts/nuget',
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = '0.1.0'
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null

$projects = Get-ChildItem -Path 'src' -Filter '*.csproj' -Recurse |
    Sort-Object FullName

foreach ($project in $projects) {
    Write-Host "Packing $($project.FullName) -> $Version"
    dotnet pack $project.FullName `
        --configuration $Configuration `
        --output $Output `
        --no-restore `
        -p:PackageVersion=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for $($project.FullName)"
    }
}

$aot = Get-ChildItem -Path $Output -Filter 'Foundgine.Aot.*.nupkg' |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' }

if ($aot.Count -ne 1) {
    throw "Expected exactly one Foundgine.Aot package, found $($aot.Count)."
}

$listing = & tar -tf $aot.FullName
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect $($aot.FullName)."
}

$required = @(
    'analyzers/dotnet/cs/Foundgine.Aot.Generator.dll',
    'lib/net9.0/Foundgine.Aot.dll',
    'lib/net9.0/Foundgine.Metadata.dll'
)

foreach ($entry in $required) {
    if ($listing -notcontains $entry) {
        throw "Package $($aot.Name) is missing $entry."
    }
}

Write-Host "NuGet packages written to $Output"
