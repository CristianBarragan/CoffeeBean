# M18.14 — Null / Empty / Cardinality Semantics

Continued directly from the M18.13 codebase.

M18.14 is deliberately a semantic safety **gate**, not another aggressive optimizer. Before
Foundgine starts rewriting `MIN`, `MAX`, `COUNT`, and more complicated aggregate expressions
into one another, the planner needs an explicit, centralized contract for the edge cases that
can make an apparently equivalent rewrite incorrect.

## New semantic contract: `SemanticAggregateSemantics`

`src/Foundgine.Semantics/Aggregates/SemanticAggregateSemantics.cs` adds an explicit,
per-aggregate semantic contract:

| Aggregate | Empty collection | NULL input | Duplicate sensitivity |
|-----------|-------------------|------------|------------------------|
| `COUNT`   | `0`               | never NULL | sensitive |
| `MIN`     | `NULL`            | ignores NULL; NULL if no non-NULL value remains | insensitive |
| `MAX`     | `NULL`            | ignores NULL; NULL if no non-NULL value remains | insensitive |

Each dimension is its own enum rather than a loose boolean or comment, so a rewrite rule (or a
test) can compare two contracts field-by-field instead of re-deriving the rule from prose:

- `SemanticEmptyCollectionResult` — `Zero` or `Null`.
- `SemanticNullInputBehavior` — `NeverNull` or `IgnoresNull`.
- `SemanticDuplicateSensitivity` — `Sensitive` or `Insensitive`.
- `SemanticCardinalityRequirement` — whether a rewrite involving this aggregate additionally
  needs a proof about relationship cardinality before it can be trusted. No aggregate in this
  milestone requires one yet; the flag exists so future aggregates (or future rewrites of
  today's aggregates) can declare it instead of the gate silently assuming `None`.

## Centralized catalog: `SemanticAggregateSemanticsCatalog`

`SemanticAggregateSemanticsCatalog` is the single source of truth for the table above:

```csharp
var semantics = SemanticAggregateSemanticsCatalog.For(SemanticFilterAggregate.Min);
// semantics.EmptyCollectionResult == SemanticEmptyCollectionResult.Null
```

Optimizer and provider code must look the contract up here instead of independently inferring
these rules. `For` throws `NotSupportedException` for an aggregate that has no registered
contract — a new aggregate can never be silently treated as COUNT/MIN/MAX-equivalent by
omission. `TryGet` is available where a non-throwing lookup is more convenient.

## New rewrite legality boundary: `AggregateRewriteLegality`

`src/Foundgine.Semantics/Aggregates/AggregateRewriteLegality.cs` adds the explicit gate a
rewrite rule must pass before it is allowed to substitute one aggregate for another. It checks,
independently:

- `CheckEmptySemantics` — does the empty-collection result match?
- `CheckNullSemantics` — does NULL-input behavior match?
- `CheckDuplicateSensitivity` — does duplicate sensitivity match?
- `CheckCardinalityRequirement` — if either side's contract requires a cardinality proof, is a
  known (non-`Unknown`) cardinality actually available? This fails closed: a rewrite that needs
  a cardinality proof is never assumed legal just because none was supplied.

`CheckSubstitution` runs all four and only reports the substitution as legal when every one of
them does. Substituting an aggregate for itself is always legal and skips the checks entirely.

```csharp
var result = AggregateRewriteLegality.CheckSubstitution(
    SemanticFilterAggregate.Count,
    SemanticFilterAggregate.Min);

// result.IsLegal == false
// result.Violations includes the empty-collection, NULL-input, and duplicate-sensitivity
// mismatches, all three at once — not just the first one found.
```

An equivalent `COUNT → COUNT` (or any aggregate to itself) transformation passes the gate
trivially, since there is nothing to compare.

Note that a *passing* `AggregateRewriteLegalityResult` is a "no known semantic difference"
certificate, not a general proof that two distinct aggregate functions compute the same value
(`MIN → MAX` passes this gate on empty/NULL/duplicate grounds, but MIN and MAX are still
different functions — a rewrite rule would need its own reason, beyond this gate, to actually
perform that substitution). The gate's job is narrower and deliberately so: it rejects rewrites
that are *provably* wrong on these four dimensions; it does not certify that a rewrite is
correct on every other dimension a future rewrite rule might depend on.

## Why this matters

A rewrite can preserve the same parent rows and still be wrong:

```text
Customer
   ↓
Orders
   ↓
MIN(Order.Amount)
```

For a customer with no orders:

- `MIN(Order.Amount)` = `NULL`
- `COUNT(Order.Amount)` = `0`

A simplistic optimizer could treat these as interchangeable when optimizing an aggregate
predicate — for example, when trying to decide whether `MIN(Order.Amount) IS NULL` and
`COUNT(Order.Amount) = 0` are the same test. They happen to coincide in this particular case
only because `MIN` ignores NULL and there are no NULL amounts to ignore; the moment NULL
`Amount` values are allowed, `MIN` can be `NULL` for a *non-empty* collection too, while `COUNT`
of the field's NULLs would not be. M18.14 gives the optimizer a structural way to catch this
class of mistake instead of relying on a reviewer noticing it in a specific rule.

## M18 aggregate safety model

The aggregate pipeline is now:

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

This is especially important for the upcoming MIN/MAX and more advanced aggregate rewrites in
M18.15, which will consult `AggregateRewriteLegality` as one of several required gates before a
rewrite is accepted.

## Deliberate non-goals

M18.14 does **not**:

- perform any aggregate rewrite itself — it only defines what would make one legal or illegal;
- change `AggregateRelationshipFilterPushdownRule` or `AggregateCardinalityOptimizationRule`,
  neither of which substitutes one aggregate function for another;
- introduce relationship-cardinality metadata or inference — `SemanticCardinalityKnowledge` is
  supplied by the caller, never derived by the semantic layer itself;
- decide *how* a legal rewrite should be expressed in the plan or compiled by a provider.

Those are the subject of M18.15.

## Fail-closed contract

Unsupported aggregate transformations must fail closed rather than relying on provider-specific
assumptions. Concretely:

- `SemanticAggregateSemanticsCatalog.For` throws for an aggregate with no registered contract,
  rather than returning a default that might be wrong.
- `AggregateRewriteLegality.CheckCardinalityRequirement` rejects a rewrite that needs cardinality
  proof whenever that proof was not supplied, rather than assuming a convenient cardinality.
- `AggregateRewriteLegalityResult.Illegal` requires at least one violation message, so a caller
  can never construct a silent, unexplained rejection (or, by omission, an accidental approval).

## Tests

- `SemanticAggregateSemanticsTests` — COUNT empty→zero, COUNT never-NULL, COUNT duplicate
  sensitivity, MIN/MAX empty→NULL, MIN/MAX NULL-input semantics, MIN/MAX duplicate
  insensitivity, catalog completeness, and fail-closed lookup behavior for unregistered
  aggregates.
- `AggregateRewriteLegalityTests` — rejecting the COUNT↔MIN semantic substitution (with each of
  the three individual violations checked explicitly), accepting self-substitution, accepting
  MIN↔MAX on empty/NULL/duplicate grounds, rejecting duplicate-sensitive → duplicate-insensitive
  rewrites, and cardinality rewrite gating (fails closed when unknown, passes once a cardinality
  proof is supplied).

## Validation

I verified:

- all `.csproj` files parse successfully;
- new source/test files pass structural checks;
- complete archive contents;
- ZIP integrity.

The archive reports:

```text
No errors detected in compressed data.
```

The environment still has no .NET SDK, so I did not falsely claim a `dotnet test` run.

## Next: M18.15 — Aggregate Rewrite Safety

Now that the semantic foundation exists, M18.15 can tackle the next genuinely difficult
optimization: safe aggregate predicate rewrites, starting with transformations where equivalence
can be formally proven.

For example, `MIN(x) > v` cannot simply become `EXISTS(x > v)` — that changes the meaning. But
there are potentially valid transformations involving `MIN`, `MAX`, `COUNT`, `SOME`, `NONE`, and
`ALL` once the relationship cardinality and NULL semantics are known.

The M18.15 gate will require all of:

- semantic equivalence
- empty-set equivalence
- NULL equivalence
- duplicate equivalence
- relationship cardinality proof
- authorization preservation
- provider capability
- cost evidence

`AggregateRewriteLegality` supplies the empty-set, NULL, duplicate, and cardinality-gating pieces
of that list; M18.15 builds the remaining proof obligations and the rule that actually performs
a rewrite once every gate passes.
