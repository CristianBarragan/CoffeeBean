# V1 port audit

The archive under `archive/FoundgineV1` is the reference implementation. This
new tree is not a namespace migration.

## Ported because it is still architecturally useful

- `EntityId`, `FieldId`, `RelationshipId`, `ColumnId`: stable metadata identity.
- `EntityMetadata`, `ColumnMetadata`, `FieldMetadata`: static domain/storage mapping.
- `RelationshipMetadata`: static relationship mapping.
- `MetadataRegistry`: simple metadata lookup boundary.
- `IMetadataProvider`: abstraction for generated/static metadata.
- Low-level column/join metadata is retained only as a future provider bridge;
  it is not consumed by the semantic graph or M4 planner.

## Reimplemented rather than copied

- `SemanticEntity`, `SemanticRelationship`, `SemanticModel`, and builders.
- `SemanticGraph` and `SemanticRequest` as the canonical semantic representations.
- `SemanticRequestResolver` for request-to-semantic topology resolution.
- `SemanticAuthorizer` for semantic authorization.
- `ExecutionPlan` and `ExecutionPlanNode` for provider-independent logical planning.

The useful V1 planning invariant that survives is: **preserve the requested
relationship tree rather than flattening it into a provider-specific join chain.**

## Deliberately not ported

- V1 `QueryIntent`, `QueryPlan`, `QueryNode`, `QueryNodeBuilder`.
- V1 `JoinGraph` as the planner's relationship authority.
- V1 SQL providers/compiler/translator.
- Graphgine/Postgres/AGE infrastructure.
- V1 mutation planning.
- GraphQL-specific concepts.
- Search/action/policy subsystems.
- Diagnostics/CQRS/Foundation helper projects.

Those can only return if a concrete Foundgine requirement demonstrates the need.

## Current acceptance target

The current path is:

`Customer -> Account -> Transaction`

represented as a semantic graph, authorized, and transformed into a
provider-independent `ExecutionPlan` without SQL or GraphQL references in the
semantic/planning layers.

## M5 — SQL provider

Ported selectively from the archived V1 SQL provider proof:

- SQL exists only after the provider-independent `ExecutionPlan`.
- SQL compilation is separate from execution.
- ADO.NET execution returns provider-neutral `ExecutionResult` rows.
- Identifier quoting is performed by the SQL provider.
- Relational join conditions use metadata `ColumnReference` values.

Not ported:

- V1 `QueryPlan` / `QueryNode` hierarchy
- V1 provider-node hierarchy
- GraphQL-specific SQL compilation
- mutation compiler
- filter/order/page compiler
- PostgreSQL-specific writer logic
- old execution/result contracts

The M5 proof is intentionally limited to the Banking read path against SQLite.

## M15 — Aggregate Filters

Ported the aggregate-filter capability as new semantic contracts rather than copying the V1 SQL/filter architecture. Collection `COUNT`, `MIN`, and `MAX` can be compared using `eq`, `neq`, `gt`, `gte`, `lt`, and `lte`; SQL renders these as correlated scalar subqueries.
