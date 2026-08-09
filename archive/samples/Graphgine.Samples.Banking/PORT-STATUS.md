# Port status: legacy/HotChocolateCoffeeBeanery -> samples/Graphgine.Samples.Banking

This document records what changed in this pass, what was already correct,
and — most importantly — exactly what remains and *why* it couldn't be
finished blind. Written from a sandbox with no .NET SDK and no NuGet access,
so nothing below was compiled or run. Treat every claim here as "read the
code carefully and reasoned about it," not "verified."

## 1. The architecture separation was already correct

Three tiers, and the project-reference graph already respects the boundary
between them:

- `legacy/HotChocolateCoffeeBeanery/` — the original monolith (Dapper +
  HotChocolate + EF + Postgres + FasterKV), with its own hand-rolled
  GraphQL/SQL compilation engine (`Domain/CoffeeBeanery/GraphQL/Core/*`:
  `NodeMap`, `SqlNodeBuilder`, `SqlQueryCompiler`, `ProcessService`, ...).
  Frozen — a reference for *behavior*, not a dependency of anything.
- `src/Foundgine.*` — the storage-agnostic platform (Metadata, Builders,
  Planning, Execution.Contracts, Providers). See
  `samples/Foundgine.Samples.Banking` for the pure-platform demo with no
  GraphQL at all.
- `src/Graphgine*` — the GraphQL product built on Foundgine
  (`Graphgine`/execution+mapping+SQL, `Graphgine.HotChocolate`/the
  HotChocolate-specific adapter layer, `Graphgine.SourceGenerators`/the
  Roslyn generators, `Graphgine.AspNetCore`/deliberately still a
  placeholder — see its own README.md).

`samples/Graphgine.Samples.Banking` already references `src/Graphgine`,
`src/Graphgine.HotChocolate` and `src/Graphgine.SourceGenerators` — **not**
the legacy hand-rolled engine. That part of the port was already done before
this pass. What was NOT done, and is not obviously flagged as incomplete
unless you read the code (this file exists to make it obvious):
`docs/11-Samples/README.md` already admits *"the Banking sample still
contains historical wiring from the Coffee Beanery implementation and must
be repaired and validated"* — this document is the detailed version of that
warning.

## 2. Fixed in this pass (concrete, high-confidence)

`Api/Api.Banking/Program.cs`:

- **Removed `services.AddGraphgine<BankingEntityContext>(connectionString)`.**
  This method does not exist anywhere in the codebase. `src/Graphgine.AspNetCore`
  — the project whose own README says this is exactly where such a method
  belongs — is an intentional placeholder (*"Left as a placeholder rather
  than guessed at, since inventing that API surface without a second real
  consumer risks getting the shape wrong"*). Someone wrote the call site
  before the callee existed. I did not invent the extension method either,
  for the same reason its own author gave; I inlined a comment explaining
  what DI registrations belong here once the mapping classes below produce
  real generated types to point at (see §3), and removed the dangling
  `using Api.Banking.Extension;` (that namespace has no files in this
  project at all).
- **Fixed argument-count mismatches in both `ResolveWith<>` calls.**
  `WrapperQueryResolver.GetWrapper` and `WrapperMutationResolver.UpsertWrapper`
  both take 4 parameters (`IProcessService<Wrapper>`, `AdapterLookup`,
  `IResolverContext`, `Wrapper`), but the lambda expressions passed only 3
  `default` arguments — `r => r.GetWrapper(default, default, default)` — which
  is a plain C# compile error (wrong overload arity), independent of anything
  generator-related. Added the missing fourth `default`.

These two are safe: they don't depend on anything unverified, just on
reading the method signatures that already exist in the sample.

## 3a. This pass: all 8 remaining mapping classes written, and one correction to §3's own approach

Continuing from the single `AccountMapping.cs` example below, this pass adds
the rest of `Api/Api.Banking/Mapping/`: `ContactPointMapping.cs`,
`ContractMapping.cs`, `CustomerMapping.cs`,
`CustomerBankingRelationshipMapping.cs`, `CustomerCustomerEdgeMapping.cs`,
`ProductMapping.cs`, `TransactionMapping.cs`, `WrapperMapping.cs` — and
simplifies `AccountMapping.cs` itself (see below). Same caveat as before:
written from a sandbox with no .NET SDK, so **none of this has been
compiled**. Every file has its own header comment with the specific
reasoning and flags; this section is the summary.

### The table in §3 said 9 models. It's 8.

`CustomerCustomerRelationship` has no `Domain.Model` class anywhere in this
sample — every field the legacy relationship model carried
(`CustomerCustomerRelationshipKey`, `InnerCustomerKey`, `OuterCustomerKey`,
`CustomerCustomerRelationshipType`) already lives directly on
`Domain.Model.CustomerCustomerEdge`. The two legacy models were collapsed
into one in this port's domain layer at some earlier point, and the legacy
`GraphMap` — oddly declared on `CustomerCustomerRelationshipMapping` rather
than the edge mapping — moves to `CustomerCustomerEdgeMapping.Graph`, where
it actually belongs. `CustomerCustomerEdgeMapping.cs` is backed by
`Database.Entity.CustomerCustomerRelationship` and does the job both legacy
classes did.

### The corrected approach: explicit `Navigations`/`Fields` are the exception, not the default

While tracing the parser closely enough to write these, it became clear
`AccountMapping.cs`'s original explicit `Navigations` block for
`Contract`/`Transaction` was unnecessary — and so would explicit `Fields`
have been, for anything whose name and type already match:

- **`EntityNavigationConvention.Resolve`** (`Passes/EntityNavigationConvention.cs`)
  walks each model's own C# properties, matches each one against another
  mapped model by name, and finds the join path by walking the FK graph
  `EntityForeignKeyEmitterGenerator` builds from `Database.Entity.Banking`'s
  EF Fluent `HasOne`/`HasMany`/`WithOne`/`WithMany`/`HasForeignKey` calls
  (confirmed — via `FluentEntityNavigationConvention.cs` — that it
  recognizes both call orderings, not just one). An explicit `Navigations`
  entry is only consulted as a *fallback* for names not already resolved
  this way.
- **`FieldMapGeneration`** (`Passes/FieldMapGeneration.cs`) does the same for
  scalar fields: same-named, type-compatible properties auto-match. Explicit
  `Fields` entries are only needed when names differ (`Customer.FirstNaming`
  → `Entity.FirstName`), an enum needs remapping, or the field genuinely has
  no correct match (see the schema gaps below).
- **`UpsertKeys`/`PrimaryKey`** are also auto-synthesized from `Entities[]`
  when left empty (`ParsePrimaryKeys`/`ParseUpsertKeys` in
  `MappingClassParser.cs`), as long as each `Entities[]` entry without an
  `AliasProperty` carries a `ModelKey` — which is `required` on
  `EntityDefinition` regardless, so this falls out for free.

So every new mapping in this pass is closer to `Entity`/`Key`
shorthand-plus-exceptions than to a full hand-declared graph. `Wrapper` is
the extreme case: `Model = typeof(Wrapper)` and nothing else — everything
it needs (`ModelChildrenInference` picking up `Wrapper.CustomerCustomerEdge`
as a `ModelChild`, `WrapperRootModelResolver` recognizing the class by name)
happens without a single explicit field. `AccountMapping.cs` was simplified
to match — its Contract/Transaction navigations were dead weight the whole
time.

### Two real schema gaps found while tracing fields — now fixed at the entity level

1. `Database.Entity.ContactPoint.CustomerKey` was `int?`; `Domain.Model.ContactPoint.CustomerKey`
   is `Guid?`. Not type-compatible under `FieldMapGeneration.AreTypesCompatible`
   (only Guid↔string, enum↔numeric, numeric↔numeric are treated as
   compatible). **Fixed**: retyped to `Guid?` in `Database.Entity/ContactPoint.cs`,
   mirroring how `Transaction` carries `AccountKey`/`ContractKey` alongside
   its int FKs. `ContactPointMapping.cs` now leaves it to convention.
2. `Database.Entity.CustomerCustomerRelationship` had no
   `InnerCustomerKey`/`OuterCustomerKey` Guid columns — only the int
   `InnerCustomerId`/`OuterCustomerId` FKs (the legacy entity had the Guid
   columns directly; `Transaction` still has the equivalent
   `AccountKey`/`ContractKey` pattern today, so this looked like an
   omission specific to this entity rather than a deliberate redesign).
   **Fixed**: added both as `Guid?` properties in
   `Database.Entity/CustomerCustomerRelationship.cs`.
   `CustomerCustomerEdgeMapping.cs`'s `Graph` block was already written
   against these column names (see the file's own header comment) and
   needed no changes once the entity caught up.

**The migration is fixed too, in place — not layered as a second migration.**
Since `20260801212658_Initial` hasn't been applied against any real database
by this sample yet (it's the only migration that exists), the correct move
is to correct its `CreateTable` column definitions directly rather than add
a second migration on top of a schema nothing has ever run against. Updated
all three files that make up this migration, consistently:
`20260801212658_Initial.cs` (`CreateTable`'s column list — `ContactPoint.CustomerKey`
now `Guid`/`uuid`; `CustomerCustomerRelationship` gained `OuterCustomerKey`/
`InnerCustomerKey`, `Guid`/`uuid`, in the same alphabetical-by-FK-pair
position EF's own generator uses elsewhere in this file), plus the matching
property declarations in `20260801212658_Initial.Designer.cs` and
`BankingEntityContextModelSnapshot.cs` (these two are near-duplicates of
each other by design — the model snapshot EF diffs future migrations
against, and the per-migration snapshot of the same point in time — so both
were changed identically). This was mechanical rather than guessed: every
line added/changed copies the exact structure of an existing, already-correct
column of the same shape elsewhere in the same file (`Transaction.AccountKey`/
`ContractKey` for the "Guid mirror alongside an int FK" pattern;
`CustomerBankingRelationship.CustomerKey` for the plain `Guid?` column
pattern) — nothing here was invented independent of a real example already
present. Not run through `dotnet ef` (no SDK in this sandbox), so treat it
as "matches every established pattern in the file it's now part of," not
"confirmed to produce a valid Postgres schema" — worth one real
`dotnet ef database update` against the `docker-compose.yml` instance before
relying on it.

A smaller third: `Domain.Model.CustomerBankingRelationship.CustomerCustomerRelationshipType`
has no equivalent on its backing entity at all (that enum belongs to
`CustomerCustomerEdge`) — reads like a copy/paste leftover on the domain
model. Non-fatal (just a stray diagnostic), left unmapped, flagged in
`CustomerBankingRelationshipMapping.cs`.

### Least-verified single piece: `ProductMapping.cs`

`Product` has no backing entity — it spans `Contract`+`Account`+`Transaction`+
`CustomerBankingRelationship`+`Customer` (five, not four — legacy's own field
map pulls `CustomerKey` from `Customer` too). This is the one case
`CompositeChildAttachmentConvention.cs` (719 lines) exists for, and it's
completely unexercised by any working example in this repo. I could trace
its *inputs* (confirmed `MappingClassParser`'s own comment on `UpsertKeys`
synthesis names `Product` as the reference example for entities without
`AliasProperty`) but not observe its output. If `Product`'s navigations
don't resolve as expected, read that pass's diagnostics first — see the
file's own header comment for the specific concern.

## 3. The real gap: a two-stage source-generator pipeline with zero working examples

`GeneratedMetadataProvider`, `GeneratedPlannerRegistry`,
`GeneratedMutationMetadataProvider`, `GeneratedEnumConversionProvider` — the
types the resolvers and DI wiring need — are not files. They're emitted at
compile time by `Graphgine.SourceGenerators`, in two stages:

1. **`EntityForeignKeyEmitterGenerator`** runs on `Database.Entity` (gated by
   `EnableEntityForeignKeyEmitter=true`, already set in that project's
   `.csproj` — this part was already wired correctly). It scans that
   project's own EF Fluent `HasOne().WithMany().HasForeignKey()` calls
   (`AccountEntityConfiguration`, `CustomerEntityConfiguration`, etc. —
   already present) and emits an assembly-level FK graph.
2. **`MappingNodeGenerator`** runs on `Api.Banking` (gated by
   `IsMappingRoot=true`, already set). It looks for non-abstract classes in
   *that project's own syntax trees* implementing
   `Graphgine.Mapping.IMappingDefinition`, and from those emits
   `GeneratedMetadataProvider`, `Planners.g.cs`, `QueryMaterializers.g.cs`,
   `MutationMaterializers.g.cs`, `AdapterTables.g.cs`, etc.

**Before this pass, zero classes implementing `IMappingDefinition` existed
anywhere in `Api.Banking` — or anywhere in this repository.** Nothing would
have been generated at all; every one of those `Generated*` types would fail
to resolve.

### What this pass adds: one worked example

`Api/Api.Banking/Mapping/AccountMapping.cs` — a complete `IMappingDefinition`
for `Account`, ported field-for-field from
`legacy/.../Domain.Shared/Mapping/AccountMapping.cs`'s old `NodeMap`/`LinkKey`
declarations into the new declarative DSL. It's syntactically valid C#
against the real `Graphgine.Mapping.MappingDefinition` record types (so it
compiles as ordinary C# regardless), and was written by tracing
`MappingClassParser.cs`'s actual syntax-tree walk (1886 lines) to match the
object-initializer shapes it expects. But **the generator's own inference
passes have no existing example anywhere in this repo to validate against**
— `ModelChildrenInference`, `CompositeChildAttachmentConvention`,
`EntityGraphChildrenInference`, `EntityNavigationConvention`,
`PlannerEmitter`, `ColumnIdResolver` are all real, substantial, and
completely unexercised by any working sample. Read the header comment on
that file before trusting it further.

### Status as of this pass: all 8 written

Same file location (`Api/Api.Banking/Mapping/`), one class per model. See
§3a above for the corrected minimal-mapping approach and the schema gaps
found while writing these.

| Model | Backing entity/entities | Status |
|---|---|---|
| `Account` | `Database.Entity.Account` | done, simplified this pass (§3a) |
| `ContactPoint` | `Database.Entity.ContactPoint` | done — `CustomerKey` type fixed this pass (§3a), migration updated in place |
| `Contract` | `Database.Entity.Contract` | done |
| `Customer` | `Database.Entity.Customer` | done |
| `CustomerBankingRelationship` | `Database.Entity.CustomerBankingRelationship` | done — one stray domain-model field flagged (§3a) |
| `CustomerCustomerEdge` | `Database.Entity.CustomerCustomerRelationship` + `Graph` block; absorbs what would've been `CustomerCustomerRelationship`'s own mapping (§3a) | done — `InnerCustomerKey`/`OuterCustomerKey` columns added this pass (§3a), migration updated in place |
| `Product` | composite: `Contract`+`Account`+`Transaction`+`CustomerBankingRelationship`+`Customer` | done — least-verified piece of this pass (§3a) |
| `Transaction` | `Database.Entity.Transaction` | done |
| `Wrapper` | model-only, no backing entity | done — minimal, see §3a |

## 4. Why `IProcessService<T>` was NOT attempted in this pass

`WrapperQueryResolver`/`WrapperMutationResolver` (already present in the
sample) depend on `IProcessService<Wrapper>`, plus `QueryRequest`,
`MutationRequest`, `PagedQueryRequest` types. **None of these exist anywhere
in `src/` or the sample** — only in `legacy/.../Domain/CoffeeBeanery/Service/ProcessService.cs`,
which talks to the old hand-rolled compiler and can't be reused as-is.

I deliberately did not author a replacement in this pass. The reason isn't
difficulty — it's a hard ordering dependency: a correct `IProcessService<T>`
needs to call into `IPlannerRegistry`/`IEntityMetaProvider`/
`IMutationMetadataProvider` (real interfaces, already defined in
`src/Graphgine/Execution/`), whose *real implementations*
(`GeneratedPlannerRegistry`, etc.) are only produced once the mapping
classes in §3 exist **and successfully compile through the actual
generator**. `Planners.g.cs`/`QueryMaterializers.g.cs`/`AdapterTables.g.cs`
are generated source I have never seen — I don't know their exact emitted
method signatures, only the hand-written interfaces they implement. Writing
`ProcessService<T>` against a shape I can't observe wouldn't be a port, it
would be a guess dressed up as one, for the piece of this system with the
least room for that (it's the part that actually talks to Postgres).

## 5. Sequenced next steps (needs a real `dotnet` + NuGet)

1. `dotnet build` on `Infrastructure/Database/Database.Entity` alone first —
   confirm the FK-graph emitter (already wired) actually produces output
   against the existing `*EntityConfiguration` classes.
2. ~~Write the remaining mapping classes~~ / ~~fix the two schema gaps~~ /
   ~~update the migration~~ — all done in this pass (§3a). What's left
   before trusting them: an actual `dotnet ef database update` (or
   `dotnet build` alone, at minimum) against the `docker-compose.yml`
   Postgres instance — the migration edits were hand-applied by mirroring
   existing patterns in the same file, not generated by `dotnet ef`, so
   they've never been run.
3. `dotnet build` on `Api.Banking` and **read what `MappingNodeGenerator`
   actually emits** — `Planners.g.cs`, `QueryMaterializers.g.cs`,
   `AdapterTables.g.cs`, `EntityMeta.g.cs`. Fix the 8 mapping classes against
   real generator diagnostics (`CBM0xx` / `EntityGraphDebug`), not against
   this document's guesses — start with `ProductMapping.cs`, the piece §3a
   flags as least-verified, and `CustomerCustomerEdgeMapping.cs`'s `Graph`
   block.
4. Only then write `IProcessService<T>` (and `QueryRequest`/`MutationRequest`/
   `PagedQueryRequest`), against the real generated types from step 3 — at
   that point it's a straightforward orchestration layer: build a
   `SelectionIR`/`MutationIR` via `HotChocolateAdapter` (already correct,
   already in the sample), hand it to `GeneratedPlannerRegistry` to get a
   `PhysicalQueryPlan`/`PhysicalMutationPlan`, execute via
   `Graphgine.Sql.PostgresSqlWriter`/`AgeConnectionFactory`, materialize rows
   via the generated materializers.
5. Uncomment the DI registrations left as comments in `Program.cs`.
6. Once it builds and runs against the `docker-compose.yml` Postgres/AGE
   instance already in this folder, update
   `docs/11-Samples/README.md` to drop the "must be repaired" warning.
