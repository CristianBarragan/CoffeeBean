# M8 — Post-M7 Core Simplification

M8 is a simplification milestone, not a GraphQL feature milestone.

## Change

`Foundgine.Semantics` no longer references `Foundgine.Metadata`.

The semantic layer already models its public contracts using stable identities and semantic types. The previous project reference was therefore an architectural dependency without a corresponding type dependency.

## Frozen dependency

```text
Foundgine.Abstractions
        ↓
Foundgine.Semantics
        ↓
Foundgine.Planning
        ↓
Foundgine.Execution
```

Metadata remains a sibling foundation consumed by components that actually need physical/domain mapping information.

## Why

The Ground-Up Porting Guide defines metadata as the description of what exists, while semantic requests and graphs describe what the caller asks for and how domain concepts relate. The semantic layer must not acquire physical/provider concepts merely because the metadata layer exists.

## Acceptance

- `Foundgine.Semantics.csproj` has no Metadata project reference.
- Semantic source files have no Metadata namespace imports.
- `SemanticRequest`, `SemanticGraph`, `SemanticModel`, resolution, authorization, filters, and ordering remain usable from stable identities and semantic types.
- No SQL, GraphQL, or provider dependency is introduced.

## Deferred

M8 does not add GraphQL fragments, aliases, variables, directives, introspection, or mutation features. Those are post-foundation capabilities and must justify themselves independently.
