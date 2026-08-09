# Current Status

## What is real

The **active tree** (`src/`, `tests/`, `samples/Foundgine.Samples.Banking` — everything
`Foundgine.sln` builds) is a minimal, self-contained five-project spine with no GraphQL and
no Graphgine anywhere in its dependency graph:

- `Foundgine.Metadata` — domain-facing `EntityMetadata`/`ColumnMetadata`/`JoinGraph`,
  independent of any query language or provider.
- `Foundgine.Builders` — `QueryPlan`/`QueryNode` (`CompositeNode`, `ProjectionNode`, ...),
  the provider-agnostic logical plan shape.
- `Foundgine.Planning` — `QueryPlanner`, the dynamic planner that turns a `QueryIntent`
  tree into a `QueryPlan` purely by consulting `MetadataRegistry`/`JoinGraph` (no
  domain-specific `if`s); also `MutationPlan`/`MutationOperation`.
- `Foundgine.Execution.Contracts` — provider-agnostic `ProviderPlan`/`ExecutionRow`/
  `IExecutionProvider`.
- `Foundgine.Providers` — `SqlPlanCompiler` (`QueryPlan` → `ProviderPlan`),
  `SqlTextTranslator` (`ProviderPlan` → SQL text, with `SqlScanNode`-occurrence-aware
  alias resolution so repeated/self-joined entities don't collide), and
  `SqlExecutionProvider` (executes against SQLite via `Microsoft.Data.Sqlite`).

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