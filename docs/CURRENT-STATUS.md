# Current Status

[Home](../README.md) → **Current Status**

## Executive status

Foundgine has a real lower-level execution proof, but the AI-native product surface is not yet implemented end to end.

The repository should therefore be treated as:

> **A working execution substrate plus an active proof of the AI-native semantic layer.**

## What is real today

The canonical Banking sample proves:

```text
Metadata
  ↓
Dynamic Planner
  ↓
Logical QueryPlan
  ↓
ProviderPlan
  ↓
SQL
  ↓
real SQLite database
  ↓
ExecutionRow result
```

The sample deliberately has no GraphQL dependency.

Current active platform projects include:

| Project | Role | Status |
|---|---|---|
| `Foundgine.Abstractions` | stable contracts | active |
| `Foundgine.Foundation` | primitives and generic CQRS contracts | active |
| `Foundgine.Metadata` | entity/column/join metadata | active |
| `Foundgine.Semantic` | Entity/Identity/Field/Relationship/Search/Action/Policy descriptors (Milestone 1) | active |
| `Foundgine.Diagnostics` | diagnostic infrastructure | active |
| `Foundgine.Builders` | logical query-plan structures | active |
| `Foundgine.Execution.Contracts` | execution/provider contracts | active |
| `Foundgine.Planning` | dynamic planning and mutation plan structures | active |
| `Foundgine.Providers` | provider compilation/execution | active, incomplete |
| `Foundgine.Samples.Banking` | canonical E2E proof | active |

## What is not yet proven

The following are target capabilities, not completed features:

- semantic entity resolution
- natural-language intent integration
- action discovery
- domain-action execution
- policy-aware planning
- preview/approval
- post-execution verification
- evidence model
- MCP adapter
- compile-time semantic domain compiler
- semantic retrieval target
- external-data execution target

## What is intentionally not being built

Foundgine is not becoming:

- an LLM provider
- a general-purpose agent framework
- a RAG framework
- a vector database
- an MCP implementation
- an ORM
- a workflow engine
- a message broker

Those are integration points.

## Evidence standard

Documentation must distinguish three states:

### Implemented

There is executable code and an automated or real integration proof.

### In progress

The architecture and partial code exist, but the full behavior is not proven.

### Planned

The capability is part of the roadmap but should not be described as existing.

Avoid "production ready", "fully AOT compatible", "zero reflection", "database independent", or performance claims until CI and benchmarks establish them.

## Immediate priorities

1. Keep the Banking E2E green.
2. ~~Introduce a protocol-neutral semantic model.~~ Done -- `Foundgine.Semantic` (Milestone 1), exercised live in the Banking sample.
3. Add deterministic entity resolution (Milestone 2).
4. Add a read-intent-to-plan path.
5. Add explicit domain actions.
6. Add policy evaluation.
7. Add preview/approval for mutations.
8. Add verification and evidence.
9. Expose the semantic surface through MCP.
10. Only then invest heavily in compile-time generation.

## Success criterion

The first meaningful product milestone is not "many features".

It is one complete read and one complete mutation:

```text
READ
"Find Ada's last five transactions."
```

and:

```text
MUTATION
"Refund Ada's last transaction."
```

Both must operate against a real application domain and produce inspectable evidence.
## What is real

The **active tree** (`src/`, `tests/`, `samples/Foundgine.Samples.Banking` — everything
`Foundgine.sln` builds) is a minimal, self-contained five-project spine with no GraphQL and
no Graphgine anywhere in its dependency graph:

- `Foundgine.Metadata` — domain-facing `EntityMetadata`/`ColumnMetadata`/`JoinGraph`,
  independent of any query language or provider.
- `Foundgine.Builders` — `QueryPlan`/`QueryNode` (`CompositeNode`, `ProjectionNode`, ...),
  the provider-agnostic logical plan shape; also `MutationPlan`/`MutationOperation`
  (`EntityMutation`/`GraphMutation`/`RelationshipMutation`), the mutation counterpart,
  living here rather than in `Foundgine.Planning` for the same reason `QueryPlan` does —
  see `ArchitectureTests`.
- `Foundgine.Planning` — `QueryPlanner`, the dynamic planner that turns a `QueryIntent`
  tree into a `QueryPlan` purely by consulting `MetadataRegistry`/`JoinGraph` (no
  domain-specific `if`s); also `MutationPlanner`, which turns a `MutationIntent` into a
  `MutationPlan` the same way, requiring a `Filter` for Update/Delete so Foundgine never
  mutates every row by accident.
- `Foundgine.Execution.Contracts` — provider-agnostic `ProviderPlan`/`ExecutionRow`/
  `IExecutionProvider`; also `ProviderMutationPlan`/`MutationResult`.
- `Foundgine.Providers` — `SqlPlanCompiler` (`QueryPlan` → `ProviderPlan`, and
  `MutationPlan` → `ProviderMutationPlan`), `SqlTextTranslator` (`ProviderPlan` → SQL
  text, with `SqlScanNode`-occurrence-aware alias resolution so repeated/self-joined
  entities don't collide; and `ProviderMutationPlan` → INSERT/UPDATE/DELETE text), and
  `SqlExecutionProvider` (executes reads against SQLite via `Microsoft.Data.Sqlite`, and
  executes a mutation plan's operations as a single SQLite transaction).

This is proven end-to-end, against a real SQLite database, not mocked, by:

- `BankingEndToEndTests` — linear `Customer -> Account -> Transaction` (**FOUND-001**)
  and a branching `Customer -> {Accounts -> Transactions, ContactPoints}` intent
  (**FOUND-002**), plus a negative test proving the planner refuses to invent a
  relationship metadata never described.
- `UglySchemaEndToEndTests` — the same branching intent against a physical schema whose
  table/column names share nothing with the domain names, proving `EntityMetadata.StorageName`/
  `ColumnMetadata.StorageName` are the only place a physical detail leaks in
  (**UGLY-SCHEMA**).
- `ProductCompositeEndToEndTests` — a five-entity linear composite,
  `Customer -> CustomerBankingRelationship -> Contract -> Account -> Transaction`,
  proving the planner/compiler/provider pipeline holds up on a chain deeper than the
  original three-table demo, plus a negative test for the same "no shortcuts" guarantee
  (**FOUND-003**).
- `MutationEndToEndTests` — Create/Update/Delete against a real SQLite database via
  `MutationIntent` → `MutationPlanner` → `MutationPlan` → `SqlPlanCompiler.CompileMutation`
  → `ProviderMutationPlan` → `SqlExecutionProvider.ExecuteMutationAsync`; also proves two
  entities' mutations submitted as one `ProviderMutationPlan` commit atomically, and that
  the planner rejects an unfiltered Update.
- `ArchitectureTests` — machine-checks the dependency-direction rules above (parses each
  `src/*.csproj`'s `<ProjectReference>`s directly) so an accidental layering violation
  fails a test instead of only being visible in a diagram.

Supporting unit-level coverage exists per project: `Foundgine.Foundation.Tests`,
`Foundgine.Metadata.Tests`, `Foundgine.Builders.Tests`, `Foundgine.Diagnostics.Tests`,
`Foundgine.Execution.Contracts.Tests`, `Foundgine.Planning.Tests`, `Foundgine.Providers.Tests`
(including `SqlPlanCompilerTests` and `SqlTextTranslatorTests`).

`archive/` holds the prior Graphgine/HotChocolate/GraphQL-fronted implementation
(`Graphgine`, `Graphgine.SourceGenerators`, `Graphgine.HotChocolate`, `Graphgine.Postgres`
usage, the CoffeeBeanery/Api.Banking samples, etc.). None of it is referenced by anything
under `src/`, `tests/`, or `samples/Foundgine.Samples.Banking` — it's historical context,
not part of the current proof.

## What is incomplete

Known incomplete areas include:

- **`ExecutionRow` cannot represent more than one occurrence of the same entity in a
  row.** `RepeatedEntityEndToEndTests` (`Employee -> Manager -> Manager`, a real
  self-join) confirms `SqlPlanCompiler`/`SqlTextTranslator`'s occurrence-aware alias
  resolution generates the one correct SQL self-join. But `ExecutionRow.Entities` is
  `IReadOnlyDictionary<ushort, object?[]>`, keyed by `EntityId` alone with no occurrence
  dimension, and `SqlExecutionProvider.ReadRow` writes every occurrence's columns into
  that one shared slot in select-list order — so the last occurrence scanned silently
  overwrites the earlier ones. In the test, only the outermost manager's ("Carol's")
  values survive; the root employee's and the middle manager's own column values are
  lost by the time a caller sees the row. SQL generation's occurrence tracking is
  validated; row materialization's is not, and needs an occurrence dimension added to
  `ExecutionRow.Entities`' key before repeated-entity/self-join queries are actually
  usable, not just plannable.
- **Benchmark evidence** for Foundgine's own pipeline costs (metadata registration,
  `JoinGraph` construction, intent construction, planning, compilation, translation,
  execution) across entity counts and shapes (linear, branching, composite, repeated
  entity). Nothing has been measured yet.
- Query filtering/ordering/paging in `SqlTextTranslator` (explicitly deferred — see its
  doc comment).
- Mutation `Upsert`, `GraphMutation`, and `RelationshipMutation` are not compiled by
  `SqlPlanCompiler` yet (each throws `NotSupportedException` — Upsert because
  INSERT ... ON CONFLICT semantics vary too much by SQL dialect to share one path with
  Create/Update/Delete). `MutationValueKind.Generated` (e.g. an AUTOINCREMENT key) and
  `.Expression` (a computed SQL expression) are likewise not yet translated by
  `SqlTextTranslator` — only `.Input`/`.Constant` carry a literal `MutationColumn.Value`
  today.
- `ModelMetadata`/`ModelEntityBinding` (a logical model backed by more than one storage
  entity) exists in `Foundgine.Metadata` but nothing in `Foundgine.Planning` or
  `Foundgine.Providers` consumes it yet — composite results today (e.g.
  `ProductCompositeEndToEndTests`) come from a `QueryIntent` over storage entities
  directly, not from planning against a registered `ModelMetadata`.
- Graph execution provider paths, cache provider paths, ASP.NET Core integration,
  analyzer project, reflection/serialization projects, formal AOT verification — all
  still archived/placeholder, not part of the active tree.
- Whether a *clean* `dotnet build`/`dotnet test` of the whole solution is currently green
  has not been reverified since the tests above were added; see "Next milestone".

## Documentation rule

When documentation says that Foundgine or Graphgine **supports** a capability, check whether it means:

1. the architecture has a contract/model for it,
2. there is partial implementation,
3. there is a complete implementation, or
4. there is a validated end-to-end path.

Only the latter two should be used for production-readiness claims.

## Milestones

| Milestone     | Status | Proof |
|---------------|--------|-------|
| FOUND-001     | DONE   | `BankingEndToEndTests` (linear Customer → Account → Transaction) |
| FOUND-002     | DONE   | `BankingEndToEndTests` (branching intent) |
| UGLY-SCHEMA   | DONE   | `UglySchemaEndToEndTests` |
| FOUND-003     | DONE   | `ProductCompositeEndToEndTests` (five-entity composite) |
| Repeated-entity / self-join E2E | DONE — found a real bug | `RepeatedEntityEndToEndTests`; see "What is incomplete" |
| Fix `ExecutionRow` occurrence collision | NOT STARTED | blocks repeated-entity queries from being actually usable |
| FOUND-004 — Validation & Benchmarking | NOT STARTED | — |

## Next milestone

1. Get a clean `dotnet build` + `dotnet test` across the whole solution and confirm it's
   actually green (not reverified since FOUND-003/the repeated-entity test landed).
2. Decide whether to fix the `ExecutionRow` occurrence-collision problem above before
   FOUND-004, or explicitly scope FOUND-004's benchmarks to shapes that don't hit it
   (no repeated entities) and track the fix separately.
3. FOUND-004: benchmark Foundgine's own pipeline costs (not a comparison against
   EF/Dapper) across 1/2/3/5-entity, branching, composite, and repeated-entity shapes.
