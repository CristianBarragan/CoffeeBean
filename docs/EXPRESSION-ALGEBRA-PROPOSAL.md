# Semantic Expression Algebra — Proposal (Not Yet Implemented)

> **Status: proposal only.** Nothing in this document exists in `Foundgine.Semantics`
> today. A previous internal write-up described this as already merged into the
> repository; that description was incorrect and has been retracted in
> `SEMANTIC-LAYER-DESIGN-NOTES.md`. Treat every section below as a design target, not
> a changelog.

This consolidates three separate review passes into one proposal, resolves the places
they disagreed or drifted, and states the condition under which it should actually be
built.

## The diagnosis (agreed across all reviews, confirmed against the code)

`Foundgine.Semantics` currently has several structurally related but independent
"mini-algebras" instead of one expression calculus:

- `SemanticFieldFilter` / `SemanticRelationshipFilter` / `SemanticAggregateFilter` /
  `SemanticAndFilter` / `SemanticOrFilter` (`Query/SemanticFilter.cs`)
- `SemanticOrderTerm` with a bolted-on `SemanticOrderAggregate` enum
  (`Query/SemanticOrder.cs`)
- Authorization predicates, composed separately under their own monotonic rules
- Mutation dependencies/effects, modeled separately again

The clearest symptom is `SemanticOrderTerm`: `COUNT` doesn't have a real field operand,
so it's represented with a nullable `FieldId` plus a side-channel aggregate enum. That's
not a bug to patch — it's evidence that ordering (and filtering, and aggregation) should
all be operations *on a common typed expression*, not five parallel special cases.

The design-notes file already commits to a graph and authorization model that are
individually strong (the authorization lattice — `A ⊓ B ≤ A`, `A ⊓ B ≤ B`, no widening
via OR — is the most mature algebraic property in the codebase today and this proposal
does not touch it). What's missing is the thing underneath filter/order/aggregate that
would let them share one set of types and one set of equivalence rules.

## Target shape

```
SemanticType
     │
SemanticValue
     │
SemanticExpression<T>
     ├── Literal
     ├── FieldReference
     ├── RelationshipReference / Path
     ├── Unary / Binary / Logical
     ├── Aggregate
     └── Function        (descriptive only — see "explicit non-goals")

SemanticFilter        = SemanticExpression<Boolean>
SemanticOrder         = SemanticExpression<T> + Direction
SemanticAggregate      = SemanticExpression<T>
AuthorizationPredicate = SemanticExpression<Boolean>
```

`SemanticExpression<T>` carries a result type so the type system — not runtime
validation — rejects nonsense like `Sum(Customer.Name)` while allowing
`Sum(Customer.Orders.Total)`.

This also surfaces a distinction none of the current filter/projection code makes:
**output projection** (fields the caller asked to see) vs. **required/working
projection** (fields the planner needs internally, e.g. a field only referenced in a
filter). These are different things today, but there is only one field-list concept in
the current IR to represent both. This split doesn't need a rename of an existing
public type — it's additive, but it is a real gap, not a cosmetic one, and should be
tracked as part of this proposal rather than solved ad hoc later.

## Equivalence / normalization

Once expressions are one type, semantic-preserving rewrites become statable and
testable instead of implicit in provider code:

```
A AND (B AND C)  ≡  A AND B AND C
A AND true       ≡  A
A OR false       ≡  A
A AND A          ≡  A
```

Each rewrite rule needs an explicit contract of what it may change and what it must
preserve — this is the generalization of the monotonicity property authorization
already has:

| Transformation            | Preserves                                   |
|----------------------------|----------------------------------------------|
| Projection pruning         | meaning, authorization, effects              |
| Predicate normalization    | result set, authorization                    |
| Predicate pushdown         | result set, cardinality, ordering, authorization |
| Authorization composition  | can only narrow authority, never widen        |
| Mutation optimization      | effects, dependencies, authorization not weaker |

## What this proposal explicitly does NOT include

Carried forward from the most recent (and most disciplined) review round, because the
restraint is correct, not because the work is finished:

- **Do not replace the existing filter API.** `SemanticFieldFilter` /
  `SemanticRelationshipFilter` / `SemanticAggregateFilter` are load-bearing for
  GraphQL, MCP, AOT, SQL, and planning. The migration path is
  `existing API → progressively becomes a projection of SemanticExpression`, not a
  rip-and-replace.
- **Do not move the optimizer into `Foundgine.Semantics`.** The semantic layer defines
  what's equivalent and what invariants must hold; the planning layer decides which
  rewrite is worth applying for a given provider. Two competing optimizers is worse
  than one slightly-generic one.
- **Do not add an executable-function escape hatch.** `Function` expressions are
  descriptive metadata, never a wrapper around an arbitrary CLR delegate. This matters
  specifically for AI/MCP-driven query construction: the IR must not be able to express
  something equivalent to `ExecuteArbitraryFunction(...)`.
- **Do not introduce a new cardinality system yet** (`ZeroOrOne` / `OneOrMany` /
  `ZeroOrMany` in place of `One` / `Many`). Correct direction eventually, but it
  interacts with relationships, nullability, authorization, GraphQL, and mutation
  dependencies in ways that deserve their own pass, not a rider on this one.

## Fuzzy / similarity scoring — further out, and explicitly gated

A later round of review sketched extending this same expression algebra with a
multi-valued truth domain (`SemanticTruth`/fuzzy truth in `[0,1]`, distinct from a
`SemanticScore` used for ranking/relevance) so that similarity search, fuzzy matching,
and ranking could sit under `SemanticFilter` / `SemanticOrder` the same way boolean
predicates do, without the provider adapters (Postgres, Elastic, etc.) defining what
the fuzzy semantics *mean*.

This is sound as a future extension **on top of** the expression algebra above, and is
explicitly out of scope until that algebra exists and is in use. The one rule from that
sketch worth adopting immediately, independent of sequencing:

> **Fuzzy or similarity-derived values must never participate in authorization.**
> Authorization stays crisp and monotonic. `similarity(user, trusted_users) > 0.8` is
> not an authorization primitive, now or later, without a deliberate, separately
> reviewed security model change.

## Trigger condition (when to actually build this)

Not a date. Build the expression algebra when a concrete feature requires shared
expression semantics — e.g., ordering by an aggregate expression, or filtering by a
computed/derived value — and the current parallel-records approach can't express it
cleanly. Until that trigger fires, the existing filter/order types and the existing
planning optimizer remain the source of truth.

## Definition of done, when it is built

The reviews converged on "prove it with tests, not more classes." Concretely:

- [ ] Semantic expression type-validation tests (illegal aggregate/type combinations
      are rejected at construction, not at the provider)
- [ ] Canonical equivalence tests for the normalization rules above
- [ ] Property tests for AND/OR normalization (associativity, idempotence, identity)
- [ ] Authorization monotonicity tests across expression rewrites (a rewrite can never
      produce a plan with more authority than the original)
- [ ] Projection dependency tests (required vs. output projection separation actually
      prevents a needed field from being pruned)
- [ ] Mutation/read shared-expression tests
- [ ] Canonical operation hashing tests, if canonicalization is included in this pass
- [ ] Adversarial test proving an optimizer rewrite cannot weaken authorization

If these can't be written and made to pass, the algebra isn't ready to replace anything
it touches, regardless of how clean the type hierarchy looks in isolation.
