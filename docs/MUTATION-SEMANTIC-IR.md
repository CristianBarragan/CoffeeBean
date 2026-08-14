# Mutation Semantic IR

Mutation semantics are represented before provider planning.

```text
Mutation Intent
    ↓
Semantic Mutation IR
    ↓
Effect Analysis
    ↓
Mutation Plan
    ↓
Execution IR
    ↓
Provider lowering
```

## Rules

- Entity identity uses `EntityId`.
- Field identity uses `FieldId`.
- Relationship identity uses `RelationshipId`.
- Physical `ColumnId` does not appear in Semantic Mutation IR.
- SQL and provider types do not appear in Semantic Mutation IR.
- Create, update, delete and upsert describe semantic intent.
- Effects describe semantic consequences.
- Dependencies describe semantic value/data-flow dependencies.
- Provider transaction mechanics are not semantic effects.
- Conflict fields are semantic fields; physical conflict columns are a later lowering concern.

The existing `Foundgine.Planning.Mutation` model remains the compatibility/physical-planning bridge during migration. It is not the canonical semantic representation.
