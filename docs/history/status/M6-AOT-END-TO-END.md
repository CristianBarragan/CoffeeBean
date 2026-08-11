# M6 — AOT End-to-End Acceptance

## Goal

Prove that compile-time generated metadata can replace hand-built relational metadata while driving the existing M1–M5 runtime path unchanged.

## Required flow

```text
Domain attributes
      ↓
Incremental source generator
      ↓
Generated MetadataRegistry
      ↓
Semantic request / resolution
      ↓
Authorization
      ↓
Provider-independent ExecutionPlan
      ↓
SqlCompiler
      ↓
SQLite execution
```

The semantic model remains a semantic/domain concern. M6 does not generate SQL, execution plans, GraphQL types, or provider nodes.

## Acceptance test

`M6AotSqlPipelineTests.Generated_metadata_drives_the_existing_M1_to_M5_pipeline` compiles attributed domain types in the test project, consumes the generated `Foundgine.Generated.GeneratedMetadata.Registry`, compiles the normal `ExecutionPlan`, and executes the resulting SQL against SQLite.

The test intentionally does not construct `EntityMetadata`, `ColumnMetadata`, or `RelationshipMetadata` itself. The generated registry is the relational metadata source.

## Architectural rule

AOT supplies the metadata implementation; it does not create a second runtime architecture.

```text
AOT → Metadata → existing M1–M5 path
```

The runtime path must not require reflection to discover the domain model.
