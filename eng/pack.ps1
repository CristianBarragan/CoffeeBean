$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/packages'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$projects = @(
  'src/Foundgine/Foundgine.csproj',
  'src/Foundgine.Abstractions/Foundgine.Abstractions.csproj',
  'src/Foundgine.Metadata/Foundgine.Metadata.csproj',
  'src/Foundgine.Semantics/Foundgine.Semantics.csproj',
  'src/Foundgine.Planning/Foundgine.Planning.csproj',
  'src/Foundgine.Execution/Foundgine.Execution.csproj',
  'src/Foundgine.Sql/Foundgine.Sql.csproj',
  'src/Foundgine.InMemory/Foundgine.InMemory.csproj',
  'src/Foundgine.Intent.Json/Foundgine.Intent.Json.csproj',
  'src/Foundgine.GraphQL.HotChocolate/Foundgine.GraphQL.HotChocolate.csproj',
  'src/Foundgine.GraphQL.HotChocolate.Mutations/Foundgine.GraphQL.HotChocolate.Mutations.csproj',
  'src/Foundgine.Aot/Foundgine.Aot.csproj'
)

dotnet build (Join-Path $root 'src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj') --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Building failed: Foundgine.Aot.Generator' }

foreach ($project in $projects) {
  dotnet pack (Join-Path $root $project) --configuration Release --output $out --no-restore
  if ($LASTEXITCODE -ne 0) { throw "Packing failed: $project" }
}
