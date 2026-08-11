# Foundgine repository recovery

This package is the cleaned post-migration working tree.

## Important boundary

`Foundgine.GraphQL.HotChocolate` is the thin read/query adapter. It must not reference `Foundgine.Planning`.

Mutation GraphQL translation lives in:

`src/Foundgine.GraphQL.HotChocolate.Mutations/HotChocolateMutationAdapter.cs`

The mutation adapter may reference `Foundgine.Planning`, `Foundgine.Execution`, and the core GraphQL adapter.

## Before building

If replacing an existing checkout, do not merge this tree over the old tree. Remove the old checkout first, or replace the entire `src/Foundgine.GraphQL.HotChocolate` directory. A stale `HotChocolateMutationAdapter.cs` in that directory will recreate the original eight compiler errors.

```powershell
cd C:\Foundgine

dotnet clean
dotnet restore
dotnet build
```

## Expected source locations

- `src/Foundgine.GraphQL.HotChocolate/HotChocolateSemanticAdapter.cs`
- `src/Foundgine.GraphQL.HotChocolate.Mutations/HotChocolateMutationAdapter.cs`
- `src/Foundgine.Planning/Mutation/*`

There must be exactly one `HotChocolateMutationAdapter.cs` in the active source tree, and it must be under `Foundgine.GraphQL.HotChocolate.Mutations`.
