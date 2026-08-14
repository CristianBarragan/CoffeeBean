# Foundgine.Abstractions

Small contracts shared across Foundgine layers.

Contains stable IDs such as `EntityId`, `FieldId`, `RelationshipId`, and `ColumnId`, plus cross-layer mutation contracts.

No SQL, GraphQL, provider, or planner implementation belongs here.

### AOT authorization predicates

Authorization expressions are reduced at build time to `AuthorizationPredicate`.
The runtime never stores or compiles an expression tree. The predicate is a
small provider-independent tree that can be carried by a semantic connection
and lowered by a provider.
## Install

```bash
dotnet add package Foundgine.Abstractions
```

## Package scope

This package intentionally contains contracts and identifiers only. It does not pull in SQL, GraphQL, EF Core, or a concrete provider.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

