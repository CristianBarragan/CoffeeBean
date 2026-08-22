# Foundgine.Planning

`Foundgine.Planning` converts an authorized semantic graph into a provider-independent execution plan.

## Execution algebra

Foundgine deliberately keeps the logical execution algebra small.

A read plan is a tree of **execution nodes**:

```text
ExecutionPlan
└── ExecutionPlanNode
    ├── Operation
    ├── Entity
    ├── Projection
    ├── Navigation
    ├── Query clauses
    ├── Authorization
    └── Children
```

The current node operations are:

| Operation | Meaning |
|---|---|
| `Scan` | Read the root semantic entity set |
| `Traverse` | Read a child entity through a resolved relationship |
| `TraverseConnection` | Read a child entity through a pre-resolved semantic connection |

These operations describe **logical topology**, not SQL.

### Clauses

The current execution model carries `SemanticQueryOptions` on the **root execution node**. A node may also carry:

- filter
- order
- limit
- offset
- cursor

These clauses are deliberately separate from `ExecutionOperation`. They describe constraints on a logical read rather than physical operations.

### Authorization

Authorization is attached to the execution node and therefore survives planning:

```text
Semantic intent
    ↓
Authorization
    ↓
ExecutionPlanNode.Authorization
    ↓
Provider compilation
```

A provider must preserve the authorization semantics when lowering the plan.

### Provider independence

The execution plan must not contain:

- SQL
- table names
- column names
- SQL aliases
- GraphQL AST nodes
- provider-specific operations
- provider-specific parameter objects

Providers lower the logical plan into their own physical representation.

```text
                 Logical execution algebra
                           │
                ┌──────────┼──────────┐
                ▼          ▼          ▼
               SQL      InMemory    Future
             provider   provider   providers
```

## Why the algebra is intentionally small

Foundgine is not trying to reproduce a database execution engine in the core.

The core answers:

> **What semantic operation is being requested, over which entities, through which semantic edges, with which constraints and authorization?**

The provider answers:

> **How should that operation be physically executed here?**

This boundary is intentional.

## What does not belong in `ExecutionOperation`

Do not add an enum member for every query feature.

For example, these are clauses, not node operations:

```text
Filter
Order
Limit
Offset
Cursor
```

Likewise, SQL-specific concepts such as:

```text
IndexSeek
HashJoin
NestedLoop
Sort
Parameter
```

must never become core execution operations.

Those belong to provider lowering.

## Mutations

Mutations use a separate planning algebra under `Foundgine.Planning.Mutation`.

Do not mix CRUD operations into the read `ExecutionOperation` enum.

The mutation algebra has its own concepts:

```text
MutationPlan
└── MutationOperation
    ├── Create
    ├── Update
    ├── Delete
    └── Upsert
```

Dependencies between mutation operations are represented explicitly rather than encoded as read-plan traversal.

## Result contract

The logical execution plan describes **what is requested**, not the final materialization format.

A provider may execute rows internally, but the execution layer owns the semantic result contract.

This distinction is important for future providers that do not naturally produce SQL-style rows.

## Design invariants

The planner must guarantee:

1. There is exactly one root node.
2. Every non-root node has exactly one navigation edge.
3. A node cannot have both a relationship and a connection edge.
4. A root cannot have a navigation edge.
5. Every parent reference points to an existing node.
6. The graph is acyclic and all nodes are reachable.
7. Provider-specific information is absent from the logical plan.
8. Authorization attached to the semantic graph is preserved in the plan.
9. Query clauses remain semantic and provider-neutral.
10. Physical execution choices are made only by providers.

These invariants define the current execution algebra.

## P0.2 evolution rule

Before adding a new `ExecutionOperation`, ask:

> Does this represent a fundamentally different kind of logical execution topology?

If the answer is no, it should probably be a clause, property, or provider concern instead.

This rule is intended to prevent `ExecutionPlanNode` and `ExecutionOperation` from becoming a catch-all abstraction.

See [docs/SECURITY.md](../../docs/SECURITY.md) for the security contract that the plan must preserve.

## Provider-aware rewrite cost

Rewrite selection may optionally consume an `IProviderCostEstimator`. The provider supplies an advisory execution-cost estimate for each candidate semantic plan. This estimate can influence ranking but never replaces semantic-equivalence or security-preservation proofs.

```text
semantic candidate
      ↓
provider cost estimate
      ↓
selection
      ↓
semantic proof + security proof
```

Provider-specific physical concepts remain outside the logical planning model.

## Cost provenance and statistics freshness

Provider cost estimates carry explicit provenance: source, optional statistics version, estimate timestamp, statistics age, and freshness state. Heuristic estimates are explicitly labelled and do not pretend to originate from live database statistics. Freshness is advisory evidence for planning quality; it never changes semantic or security requirements.

### Predicate pushdown

The planner includes the conservative `predicate.pushdown.disjunction` rule. It applies bounded Boolean distributivity while preserving semantic equivalence and security invariants.

## Relationship traversal optimization

Relationship traversal nodes can carry optional cardinality metadata and receive a provider-neutral `SingleHop` or `SetBased` traversal hint. The hint is physical-plan metadata and is not treated as semantic meaning.


### Aggregate cardinality optimization

The planner exposes the `aggregate.cardinality.short-circuit` rule. It adds physical hints for provably equivalent COUNT emptiness tests while leaving semantic filters unchanged.
