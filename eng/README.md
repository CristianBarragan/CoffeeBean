# Packaging

Foundgine is split into focused NuGet packages. The benchmark applications and test projects are not packable.

## Public packages

- `Foundgine` — main semantic execution facade.
- `Foundgine.Abstractions` — provider-independent contracts.
- `Foundgine.Metadata` — semantic metadata.
- `Foundgine.Semantics` — intent, resolution and authorization.
- `Foundgine.Planning` — provider-independent planning.
- `Foundgine.Execution` — execution boundary.
- `Foundgine.Sql` — SQL provider.
- `Foundgine.InMemory` — in-memory provider.
- `Foundgine.Intent.Json` — JSON intent adapter.
- `Foundgine.GraphQL.HotChocolate` — GraphQL query adapter.
- `Foundgine.GraphQL.HotChocolate.Mutations` — GraphQL mutation adapter.
- `Foundgine.Aot` — AOT attributes/runtime plus the source generator packaged as an analyzer.

`Foundgine.Aot.Generator` is intentionally not a standalone package. Consumers receive it through `Foundgine.Aot`.
