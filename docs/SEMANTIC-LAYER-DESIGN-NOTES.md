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

The review proposed a full `SemanticExpression` hierarchy, structural graph sharing,
a richer relationship cardinality algebra, and a separate semantic optimizer and
canonicalizer. These are good long-term directions, but implementing them all now
would duplicate existing planning infrastructure and expand the public API before
there is a demonstrated need.

The next architectural step should be a formal semantic-expression algebra only when
query and mutation features actually require shared expression semantics. The existing
planning optimizer should remain the optimization owner until then.
