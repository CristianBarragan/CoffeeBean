# Foundgine.Sql

`Foundgine.Sql` is the SQL provider for Foundgine.

It lowers provider-independent execution IR/plans into parameterized SQL and executes them through ADO.NET. PostgreSQL-specific capabilities are implemented here rather than in the semantic or planning layers.

## Responsibility

```text
SemanticPlan / ExecutionIR
          ↓
      Foundgine.Sql
          ├── SQL compilation
          ├── SQL authorization lowering
          ├── parameter binding
          ├── provider security conformance
          └── ADO.NET execution
          ↓
       PostgreSQL / SQL database
```

The package owns physical SQL concerns.

## Query execution

`SqlCompiler` converts Foundgine execution IR into a `SqlPlan`.

The compiler uses structural metadata for physical correspondence:

```text
Semantic Entity/Field
        ↓
metadata mapping
        ↓
table/column
        ↓
parameterized SQL
```

External names are not treated as raw SQL identifiers.

## `SqlExecutionProvider`

`SqlExecutionProvider` executes compiled SQL using a supplied `DbConnection` and optional `DbTransaction`.

```csharp
var provider = new SqlExecutionProvider(connection, transaction);

var result = await provider.ExecuteAsync(
    providerPlan,
    executionContext,
    cancellationToken);
```

Connection lifetime remains an application concern.

## PostgreSQL retrieval

`PostgresRetrievalCandidateSource` implements the semantic candidate-retrieval boundary for PostgreSQL.

It can provide ranked candidates and provenance evidence for ambiguous semantic references.

Supported strategies include:

| Strategy | PostgreSQL mechanism |
|---|---|
| `Fuzzy` | `pg_trgm` |
| `FullText` | `tsvector` / `websearch_to_tsquery` |
| `Search` | optional `pg_search` / BM25 |
| `GraphSimilarity` | optional Apache AGE |
| `Vector` | intentionally reserved for a future `pgvector` provider |

Retrieval is grounding/evidence, not authorization. Final semantic resolution, authorization, and execution remain deterministic Foundgine operations.

## SQL authorization

`SqlAuthorizationWriter` lowers provider-independent authorization predicates into SQL-safe expressions and runtime parameters.

The intended flow is:

```text
semantic authorization predicate
        ↓
logical execution plan
        ↓
ExecutionIR
        ↓
SqlAuthorizationWriter
        ↓
parameterized SQL
```

Authorization context values remain runtime parameters.

## Security conformance

`SqlSecurityConformance` checks that the compiled SQL plan preserves the security invariants required by the execution IR.

A failed conformance check should prevent execution.

This is intentionally separate from normal SQL syntax validation: a statement can be valid SQL and still be an invalid Foundgine execution if it drops a required security obligation.

## Pagination and cursors

`CursorCodec` provides the cursor serialization boundary used by cursor pagination.

Cursors are treated as opaque values at the transport boundary and decoded into typed semantic values during SQL execution.

Ordering and cursor semantics originate in the semantic/planning layers; SQL only implements the already-authorized plan.

## Mutation compilation

The package contains separate mutation compilers:

- `SqlMutationCompiler`;
- `PostgresBatchedMutationCompiler`.

The mutation compiler lowers the provider-independent mutation plan into SQL mutation plans.

Supported mutation kinds include:

```text
Create
Update
Delete
Upsert
```

### PostgreSQL batched mutations

`PostgresBatchedMutationCompiler` can compile supported mutation batches into PostgreSQL-oriented set-based statements.

The compiler owns provider-specific details such as:

- `unnest`-based input shaping;
- grouping compatible operations;
- generated-key correlation;
- SQL CTE structure;
- PostgreSQL parameter binding.

Those details are deliberately not exposed in the semantic mutation graph.

## Mutation execution

`SqlMutationExecutionProvider` executes SQL mutation plans.

`PostgresBatchedMutationExecutionProvider` handles the PostgreSQL batched path.

Mutation execution remains behind `Foundgine.Execution` security/conformance boundaries.

## Provider-aware cost estimation

`SqlCostEstimator` implements `IProviderCostEstimator`.

Its estimates are advisory inputs to planner rewrite selection. They must never override semantic equivalence or authorization preservation.

Cost metadata can identify whether an estimate is heuristic or based on provider statistics.

## SQL plan structure

`SqlPlan` contains the provider-specific SQL representation.

It may include:

- SQL text;
- parameter bindings;
- pagination information;
- provider metadata.

This is exactly the kind of information that must **not** appear in `Foundgine.Planning`.

## Database support

The package is primarily designed around PostgreSQL behavior and Npgsql.

Applications should treat PostgreSQL-specific features as provider capabilities rather than semantic guarantees.

## What this package does not do

It does not:

- define the semantic model;
- authorize callers;
- parse GraphQL;
- parse JSON intent;
- call an LLM;
- act as an ORM;
- manage migrations;
- infer application meaning from arbitrary SQL.

## Recommended PostgreSQL stack

```text
Foundgine
   ↓
Foundgine.Semantics
   ↓
Foundgine.Planning
   ↓
Foundgine.Execution
   ↓
Foundgine.Sql
   ↓
Npgsql / PostgreSQL
```

Structural metadata normally comes from `Foundgine.Metadata`, optionally generated through the AOT pipeline.

## Testing

The repository includes provider/integration coverage for PostgreSQL. See:

- `docs/POSTGRES-E2E.md`
- `tests/Foundgine.E2E.Tests`

The normal unit suite does not require a database.

## Target framework

- .NET 9
- Npgsql-based SQL execution
- MIT licensed
