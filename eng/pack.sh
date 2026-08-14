#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
mkdir -p "$OUT"

dotnet restore "$ROOT/Foundgine.sln"
dotnet build "$ROOT/Foundgine.sln" --configuration Release --no-restore

projects=(
  src/Foundgine/Foundgine.csproj
  src/Foundgine.Abstractions/Foundgine.Abstractions.csproj
  src/Foundgine.Metadata/Foundgine.Metadata.csproj
  src/Foundgine.Semantics/Foundgine.Semantics.csproj
  src/Foundgine.Planning/Foundgine.Planning.csproj
  src/Foundgine.Execution/Foundgine.Execution.csproj
  src/Foundgine.Sql/Foundgine.Sql.csproj
  src/Foundgine.InMemory/Foundgine.InMemory.csproj
  src/Foundgine.Intent.Json/Foundgine.Intent.Json.csproj
  src/Foundgine.GraphQL.HotChocolate/Foundgine.GraphQL.HotChocolate.csproj
  src/Foundgine.GraphQL.HotChocolate.Mutations/Foundgine.GraphQL.HotChocolate.Mutations.csproj
  src/Foundgine.Aot/Foundgine.Aot.csproj
)
for project in "${projects[@]}"; do
  dotnet pack "$ROOT/$project" --configuration Release --output "$OUT" --no-restore
done
