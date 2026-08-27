# Semantic Layer Design Notes

This iteration tightens the semantic contracts without replacing the existing
Foundgine architecture.

## Decisions

### 1. Resolution is a correctness boundary

The read pipeline is now explicitly:

`Resolve -> Validate -> Normalize -> Semantic IR -> Authorization -> Planning`

Resolution validates the graph against the semantic model before returning it.

### 2. Semantic types are additive

`SemanticType` provides provider-independent type meaning while `SemanticField.ClrType`
remains available for existing provider adapters. Removing CLR metadata immediately
would create unnecessary API churn and would not improve the core architecture enough
to justify a breaking release.

### 3. Field capabilities live in the semantic model

Fields can declare filter, sort, selection, aggregation, write, computed, sensitive,
and deprecated capabilities. Existing fields default to the legacy-compatible
read/filter/sort/aggregate surface. New models can narrow this explicitly.

### 4. Query controls are validated before planning

Negative limits/offsets, invalid cursor combinations, and duplicate ordering terms
are rejected by the semantic layer. Cursor pagination is normalized with the root
identity as a deterministic tie-breaker.

### 5. COUNT remains compatible while becoming semantically canonical

The current `SemanticOrderTerm` retains a `FieldId` for API compatibility. COUNT does
not actually have a target-field operand, so resolution canonicalizes COUNT to the
target entity identity. A future breaking API can replace this nullable operand with
a discriminated order expression without forcing that change into this release.

### 6. The semantic model is immutable after Build

`SemanticModelBuilder` remains mutable, but `Build()` now returns a snapshot. This
makes model sharing, concurrency, versioning, and caching safer.

### 7. Graph authorization no longer rebuilds topology

`WithAuthorization()` uses record replacement and preserves node identities instead
of reconstructing the entire graph. Connection edges are also preserved by the
semantic authorizer.

### 8. Security context propagation is explicit

`ReadIntentCompiler` now carries `SecurityExecutionContext` into `SemanticRequest`.
Warrant verification remains in the engine because it depends on host key resolution,
replay protection, and execution policy. The semantic resolver therefore does not
silently become a transport/security-token verifier.

## Deliberately deferred

Later review rounds proposed a full `SemanticExpression` hierarchy, a `SemanticValue`
type, canonicalization/hashing, an output-vs-required-projection split, a general
order-expression form, and (further out) a typed fuzzy-truth/scoring extension on top
of that algebra.

**Status: none of this is implemented.** `Query/SemanticFilter.cs` and
`Query/SemanticOrder.cs` are unchanged from what's described above — `SemanticFieldFilter`,
`SemanticRelationshipFilter`, `SemanticAggregateFilter`, and `SemanticOrderTerm` (with its
side-channel `SemanticOrderAggregate` enum) remain the only representation. A prior
write-up of this proposal narrated it as already merged into the codebase; that was
inaccurate and should not be repeated or cited as a completed change.

The full proposal, including what it does and does not include and the specific
conditions under which it should be built, now lives in
[`EXPRESSION-ALGEBRA-PROPOSAL.md`](./EXPRESSION-ALGEBRA-PROPOSAL.md). The short version
carries forward unchanged from before: implement a formal semantic-expression algebra
only when a concrete query or mutation feature actually requires shared expression
semantics (e.g. ordering by an aggregate, or filtering by a computed expression). Until
that trigger fires, the existing filter/order records stand and the existing planning
optimizer remains the optimization owner.
