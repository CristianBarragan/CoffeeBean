# V1 port audit

The archive under `archive/FoundgineV1` is the reference implementation. This
new tree is not a namespace migration.

## Ported because it is still architecturally useful

- `EntityId`, `FieldId`, `RelationshipId`, `ColumnId`: stable metadata identity.
- `EntityMetadata`, `ColumnMetadata`, `FieldMetadata`: static domain/storage mapping.
- `RelationshipMetadata`: static relationship mapping.
- `MetadataRegistry`: simple metadata lookup boundary.
- `IMetadataProvider`: abstraction for generated/static metadata.
- `ColumnReference`, `JoinCondition`, `JoinMetadata`, `JoinKind`: retained as
  low-level metadata primitives for the future provider bridge; they are not
  part of the semantic graph.
- `StorageEntityId`: keeps logical entity identity separate from physical
  storage identity.
- The minimal `EntityResolver`/candidate-source pattern: retained because V1
  proved the useful invariant that resolution must never invent identities.
- `ResolutionResult`, `ResolvedReference`, and evidence: retained in reduced
  form because resolution is a core Foundgine responsibility.

## Reimplemented rather than copied

- `SemanticEntity`, `SemanticRelationship`, `SemanticModel`, and builders.
  These are based on the useful V1 idea but are deliberately smaller.
- `SemanticGraph` and `SemanticRequest` are new canonical representations.
- `ExecutionPlan` is new and intentionally provider-independent.
- Execution contracts are new and minimal.

## Deliberately not ported

- V1 `QueryIntent`, `QueryPlan`, `QueryNode`, `QueryNodeBuilder`.
- V1 SQL providers/compiler/translator.
- Graphgine/Postgres/AGE infrastructure.
- V1 mutation planning.
- GraphQL-specific concepts.
- Search/action/policy subsystems.
- Diagnostics/CQRS/Foundation helper projects.
- The old graph/join planning model.

Those can only return if a concrete Foundgine requirement demonstrates the need.

## Current acceptance target

The first target is:

`Customer -> Account -> Transaction`

represented as a semantic graph and transformed into a provider-independent
`ExecutionPlan`, without SQL or GraphQL references in the semantic/planning
layers.
