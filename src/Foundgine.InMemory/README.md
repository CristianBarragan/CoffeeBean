# Foundgine.InMemory

A deliberately small execution provider used to prove that Foundgine's execution plan is genuinely provider-independent.

It executes the same `Foundgine.Planning.ExecutionPlan` used by the SQL provider directly over CLR-backed rows. It does not generate SQL, use EF Core, or depend on a database engine.

This provider is intentionally limited. It supports the read/traversal/filter/order/page subset needed to validate the architectural boundary. It is a proof provider, not a replacement for a production data store.
## Install

```bash
dotnet add package Foundgine.InMemory
```

## Package scope

This is a proof/development provider. It is intentionally smaller than the SQL provider and is used to demonstrate that the same logical execution plan can be consumed without SQL.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

