# M18.15 — Aggregate Rewrite Safety (Proof Gate)

Continued directly from the M18.14 codebase.

M18.14 gave the planner a way to *ask* whether substituting one aggregate for another is
semantically legal (`AggregateRewriteLegality`), but it deliberately stopped short of anything
that could actually authorize a rewrite end-to-end. M18.15 closes that gap with a single
composite proof gate — `AggregateRewriteProof` — that combines every obligation the roadmap
called for:

```text
Relationship
      ↓
Cardinality
      ↓
Empty semantics
      ↓
NULL semantics
      ↓
Duplicate semantics
      ↓
Aggregate rewrite
      ↓
Semantic equivalence
      ↓
Security preservation
      ↓
Provider cost
      ↓
Execution
```

## New composite proof: `AggregateRewriteProof`

`src/Foundgine.Planning/AggregateRewriteProof.cs` is the fail-closed gate a caller must pass
through `Create` before an aggregate substitution can be trusted:

```csharp
var proof = AggregateRewriteProof.Create(
    before,                 // SemanticPlan prior to the rewrite
    after,                  // SemanticPlan after the rewrite
    SemanticAggregateSemanticsCatalog.Count,   // "from" aggregate contract
    SemanticAggregateSemanticsCatalog.Count,   // "to" aggregate contract
    AggregateCardinalityProof.FromCardinality(RelationshipCardinality.Many),
    AggregateProviderCapabilityRegistry.GenericSql,
    ProviderCostEstimate.From("sql", 1.0d));

// proof.IsSatisfied == true only if every dimension below held.
```

`Create` evaluates, in order, and throws `InvalidOperationException` at the first violation
rather than returning a proof a caller might forget to check:

1. **Semantic equivalence** (`SemanticEquivalenceProof`) — does the rewritten plan still mean
   the same provider-neutral thing? Checked first: nothing else matters if this fails.
2. **Provider capability** (`AggregateProviderCapability`) — did the target provider actually
   declare it can evaluate the resulting aggregate? A rewrite that is semantically legal
   everywhere can still be un-executable on a specific provider that never claimed support for
   it.
3. **Aggregate legality** — empty-collection, NULL-input, and duplicate-sensitivity
   equivalence, plus the cardinality-proof requirement, all via the M18.14
   `AggregateRewriteLegality` gate. Violations from all four checks are collected and reported
   together, not just the first one found.
4. **Authorization preservation** (`AuthorizationPreservationProof`, new in this milestone) —
   does the rewritten plan still require every security invariant the source plan required? A
   rewrite may add invariants but must never silently drop one.

The provider's `ProviderCostEstimate` is threaded through and carried on the resulting proof for
provenance, but — consistent with `ProviderCostEstimate`'s own contract — it is advisory only:
it never weakens any of the four gates above.

## New supporting types

- `src/Foundgine.Semantics/Aggregates/AggregateCardinalityProof.cs` — the single sanctioned
  bridge from a structurally-proven `RelationshipCardinality` (`One`/`Many`) to the
  `SemanticCardinalityKnowledge` vocabulary `AggregateRewriteLegality` already understands
  (`AtMostOne`/`Unbounded`). This exists so a cardinality claim is always traceable to an actual
  structural proof rather than ad-hoc reasoning at a call site.
- `src/Foundgine.Semantics/Aggregates/AggregateProviderCapability.cs` — a provider's declared
  support for specific aggregates, aggregate predicates, and relationship quantifiers, plus an
  `AggregateProviderCapabilityRegistry.GenericSql` baseline entry supporting COUNT/MIN/MAX.
- `src/Foundgine.Planning/AuthorizationPreservationProof.cs` — checks that no security invariant
  required by the plan before rewriting has been dropped after rewriting.

## What this milestone deliberately does not do

`AggregateRewriteProof` is a proof gate, not a rewrite rule. It does not:

- decide *which* aggregate substitutions are worth attempting;
- implement the `IPlanRewriteRule` contract or register anything with `SemanticPlanOptimizer`;
- perform the `SOME`/`NONE`/`ALL` predicate rewrites described in the roadmap's M18.15 entry.

As M18.14 already notes, a *passing* legality result is a "no known semantic difference"
certificate between two aggregates' edge-case behavior — not proof that two distinct aggregate
functions compute the same value. `MIN ↔ MAX` passes the empty/NULL/duplicate checks but MIN and
MAX are still different functions; nothing in this milestone licenses a rule to actually
interchange them. Writing the rule that picks specific, provably-correct rewrites (e.g. certain
`COUNT`-existence predicates collapsing into relationship quantifiers, continuing the M18.13
direction) is follow-on work once a concrete, semantically-correct transformation is identified.

## Tests

`tests/Foundgine.Planning.Tests/AggregateRewriteProofTests.cs` (composite gate):

- self-substitution with a known provider satisfies every dimension of the proof;
- `COUNT → MIN` is rejected even when cardinality and provider are both known, with the
  empty-collection, NULL-input, and duplicate-sensitivity violations all reported;
- a provider that never declared support for the resulting aggregate rejects the rewrite;
- a semantic-equivalence violation is reported ahead of aggregate-specific checks;
- a rewrite that drops a required security invariant is rejected.

`tests/Foundgine.Semantics.Tests/AggregateCardinalityProofTests.cs`:

- `One` derives `AtMostOne`, `Many` derives `Unbounded`, `Unknown` carries no knowledge;
- derived knowledge satisfies `AggregateRewriteLegality.CheckCardinalityRequirement` when a
  requirement exists, and `Unknown` still fails it closed.

`tests/Foundgine.Semantics.Tests/AggregateProviderCapabilityTests.cs`:

- `GenericSql` supports every catalogued aggregate plus aggregate predicates and relationship
  quantifiers;
- a narrower, caller-supplied capability only supports the aggregates it explicitly declared;
- a capability with no declared aggregates supports nothing.

`tests/Foundgine.Planning.Tests/AuthorizationPreservationProofTests.cs`:

- identical invariant sets are preserved; adding an invariant is not a regression;
- dropping one or every required invariant is rejected, with a violation reported per dropped
  invariant;
- no required invariants on either side is trivially preserved.

## Validation

The sandbox this was written in has no .NET SDK available (no network access to install one), so
I traced every test case above against the implementation by hand — constructors, enum
`ToString()` values, exact exception-message substrings, and how the existing
`SemanticEquivalenceFingerprint` already folds security invariants into plan equivalence (which
is why the security-regression test can throw at the semantic-equivalence step rather than the
authorization-preservation step and still satisfy the test as written). I did not run
`dotnet test`; that should be done before merging.

## Follow-on: the SQL provider now actually consumes the COUNT-existence hint

The `## Next` section originally here proposed collapsing bare `COUNT(R) > 0` into a
`SOME(R, …)` relationship quantifier as an `IPlanRewriteRule`. Investigating that concretely
surfaced two independent blockers, not one:

1. **No IR representation for "no predicate".** `SemanticRelationshipFilter.Predicate` is
   non-nullable, so replacing a predicate-less `COUNT` with `SOME` requires inventing an
   "always true" filter node — a new case in every exhaustive switch over
   `SemanticFilterExpression` (`SemanticQuerySqlWriter`, `SemanticEquivalenceFingerprint`,
   `SemanticPlanFingerprint`, `SemanticFilterValidator`, `MutationPlanner`,
   `MutationAuthorizer`, and more).
2. **The generic rewrite gate can't express the equivalence anyway.**
   `SemanticPlanOptimizer.ApplyRule`/`Optimize` unconditionally require
   `SemanticEquivalenceFingerprint.Create(before) == Create(after)` (ordinal string equality)
   for *every* `IPlanRewriteRule`. That fingerprint normalizes AND/OR commutativity and DNF,
   but has no notion that an `aggregate(...)` token and a `relationship(...)` token can denote
   the same boolean value. Teaching it that would weaken a structural-equivalence check every
   other rule in the system also leans on — too broad a change to make as a side effect of one
   rule, and still an open design question.

Both are real product decisions, not implementation details, so the quantifier rewrite remains
future work pending that design pass.

What *was* concretely actionable: `AggregateCardinalityOptimizationRule` (M18.12) already
proves exactly this equivalence and records it as an `AggregateExecutionStrategy` hint
(`CountExistsShortCircuit` / `CountEmptyShortCircuit`) on the plan node — without touching the
semantic filter shape, so neither blocker above applies to it. Tracing where that hint went,
it turned out `ExecutionIRNode.From` never copied it off `SemanticPlanNode`, so the hint was
silently discarded at the `SemanticPlan → ExecutionIR` lowering boundary and no provider ever
saw it. It was plumbed but inert.

This is now fixed, and the SQL provider consumes it:

- `src/Foundgine.Execution/ExecutionIR.cs` — `ExecutionIRNode` now carries
  `AggregateExecutionStrategy` through `From`.
- `src/Foundgine.Planning/AggregateExecutionStrategyResolver.cs` (new) — the "does this bare
  `COUNT` comparison reduce to an emptiness/existence test" derivation, extracted out of
  `AggregateCardinalityOptimizationRule` into one shared, public place, plus an
  `IsEligibleFor(filter, nodeStrategy)` check so a provider only rewrites the specific
  aggregate filters that earned the node's hint — not every aggregate filter sharing that
  node. `AggregateCardinalityOptimizationRule` now delegates to this instead of duplicating
  the logic.
- `src/Foundgine.Sql/Query/SemanticQuerySqlWriter.cs` — threads the node's
  `AggregateExecutionStrategy` through filter compilation; an eligible bare `COUNT` filter now
  renders as `EXISTS (...)` / `NOT EXISTS (...)` instead of a `(SELECT COUNT(*) ...) op @p`
  scalar comparison. Everything else renders exactly as before.
- `src/Foundgine.Sql/SqlCompiler.cs` — passes `root.Node.AggregateExecutionStrategy` into the
  writer.

No change to `SemanticFilterExpression`, `SemanticEquivalenceFingerprint`, or the optimizer's
proof gates. This stays entirely inside "physical execution hint," which is what
`AggregateExecutionStrategy` was already documented to be — it does not touch `IPlanRewriteRule`
or `AggregateRewriteProof` and does not need to.

### Tests (follow-on)

`tests/Foundgine.Planning.Tests/AggregateExecutionStrategyResolverTests.cs`:

- the exists/empty short-circuit derivations and the exact-count cases that must not resolve;
- eligibility correctly requires a bare filter (no field, no nested predicate) whose own
  comparison agrees with the node's strategy, not merely a non-default node strategy.

`tests/Foundgine.E2E.Tests/AggregateExistenceSqlRenderingTests.cs`:

- `COUNT(R) > 0` optimized then compiled renders `EXISTS`, not `COUNT(*)`;
- `COUNT(R) = 0` optimized then compiled renders `NOT EXISTS`, not `COUNT(*)`;
- `COUNT(R) > 1` (not eligible) keeps `AggregateExecutionStrategy.Default` and still compiles
  to `COUNT(*)`;
- compiling the un-optimized plan directly (rule never run) is byte-for-byte the same
  `COUNT(*)` rendering as before this change — no behavior change for callers that don't run
  the optimizer.

### Validation (follow-on)

Same sandbox constraint as above: no .NET SDK, no network access to install one. I hand-traced
every call site touched by the `SemanticQuerySqlWriter.WriteFilter` signature change
(`WriteWhere`, `WriteRelationshipFilter`, `WriteAggregateFilter`, `Join`) and confirmed
`ExecutionIRNode`'s new trailing optional parameter doesn't break the two existing 8-arg
construction sites in `M175SqlSecurityConformanceTests.cs`. `dotnet build && dotnet test`
should still be run before merging.

## Next

The quantifier-rewrite design questions above remain open:

- how to represent "no predicate" in `SemanticFilterExpression` (new always-true node type vs.
  a nullable `Predicate`), and
- whether/how to let `SemanticPlanOptimizer` accept a rule-specific equivalence proof (e.g.
  `AggregateRewriteProof`) instead of always requiring exact fingerprint equality.

Both need a deliberate design pass rather than a decision made as a side effect of shipping one
rewrite rule.
