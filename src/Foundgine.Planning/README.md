# Foundgine.Planning

`Foundgine.Planning` converts an authorized semantic operation into a provider-independent execution plan.

The planner is deliberately **logical**. It describes what must be executed without embedding SQL, database tables, SQL aliases, provider parameters, or transport-specific objects.

## Planning pipeline

```text
Semantic request
      ↓
Resolved semantic operation
      ↓
Authorization
      ↓
Semantic plan
      ↓
Security-preserving rewrites
      ↓
Execution IR
      ↓
Provider compiler
```

The planner is therefore the boundary between application meaning and physical execution.

## Authorization binding across planning

Planning starts from an authorized semantic operation, not from raw transport input. When the planner is given a trusted `SemanticContractSnapshot` and `SemanticAuthorizationResult`, it validates contract membership, builds the provider-independent plan, and attaches `SemanticPlanAuthorizationBinding`.

```text
SemanticOperationGraph
        ↓
 authorization
        ↓
AuthorizedGraph + Evidence
        ↓
      Planner
        ↓
   SemanticPlan
        │
        └── AuthorizationBinding
        ↓
 security-preserving rewrites
        ↓
   ExecutionIR
```

The binding records the exact contract fingerprint and authorization fingerprint. It is immutable provenance connecting the logical plan to the security decision that permitted it.

`SemanticPlanAuthorizationBindingProof` is used when rewrites are evaluated to ensure that optimization did not add, remove, or substitute authorization provenance. Semantic equivalence alone is insufficient: a rewrite must preserve authority as well as meaning.

## Read execution algebra

The core read representation is a tree of semantic plan nodes:

```text
SemanticPlan
└── SemanticPlanNode
    ├── Operation
    ├── Entity
    ├── Projection
    ├── Navigation
    ├── Query clauses
    ├── Authorization
    └── Children
```

The current logical operations are intentionally small:

| Operation | Meaning |
|---|---|
| `Scan` | Read the root semantic entity set |
| `Traverse` | Visit a child through a resolved relationship |
| `TraverseConnection` | Visit a child through a resolved semantic connection |

These are topology operations, not physical database operations.

## Clauses are not operations

Filtering, ordering, pagination, and cursor controls are semantic constraints.

They should not become physical operation enum members such as:

```text
IndexSeek
HashJoin
NestedLoop
Sort
```

Those decisions belong to providers.

## Planner invariants

A valid plan maintains structural invariants including:

1. exactly one root;
2. non-root nodes have a parent/navigation relationship;
3. root nodes do not have a parent navigation edge;
4. parent references point to existing nodes;
5. the graph is acyclic and reachable;
6. relationship/connection topology is coherent;
7. provider-specific data is absent;
8. authorization is preserved;
9. query clauses remain semantic;
10. physical execution strategy is left to the provider.

These invariants are more important than adding more planner operations.

## `Planner`

The `Planner` can plan semantic contracts/operations and semantic operation graphs.

The result is a `SemanticPlan` that can be inspected, fingerprinted, optimized, and compiled by a provider.

## Optimization

Foundgine's optimizer is conservative.

A rewrite is not accepted merely because it looks faster. It must preserve semantic meaning and security.

The planning layer includes rules such as:

- `AuthorizationCanonicalizationRule`;
- `PredicatePushdownRule`;
- `ProjectionPruningRule`;
- `RelationshipTraversalOptimizationRule`;
- `RelationshipJoinOrderingRule`;
- `AggregateRelationshipFilterPushdownRule`;
- `AggregateExistenceCollapseRule`;
- `AggregateCardinalityOptimizationRule`.

Rules can expose preconditions, cost/benefit information, idempotence, priorities, and security obligations.

## Rewrite proof model

The optimizer uses explicit proof records around accepted rewrites.

Conceptually:

```text
Before
  ↓
candidate rewrite
  ↓
semantic equivalence proof
  +
authorization preservation proof
  +
aggregate/cardinality/null legality where required
  +
provider capability where required
  ↓
After
```

A rewrite that cannot prove its obligations should be rejected.

## Authorization canonicalization

Authorization predicates can be normalized deterministically.

The goal is to make semantically equivalent predicates produce stable plan shapes without changing authority.

The optimizer must never:

- evaluate caller authorization itself;
- remove a predicate because a provider claims it is redundant;
- widen access;
- turn a conditional policy into an unconditional one.

## Predicate pushdown

`PredicatePushdownRule` provides conservative Boolean normalization/pushdown where semantic equivalence can be maintained.

Provider-specific pushdown remains a provider compilation concern.

## Projection pruning

Projection pruning currently focuses on safe redundancy removal while preserving requested output order and tracking fields required by filters/order expressions.

The semantic model intentionally distinguishes neither all internal working fields nor all provider projections, so the optimizer does not perform aggressive dead-field elimination that could alter the requested result.

## Relationship traversal optimization

Relationship traversal can carry provider-neutral metadata such as cardinality and a `SingleHop`/`SetBased` hint.

The hint is not semantic meaning. A provider may use it when selecting a physical strategy, subject to security and semantic constraints.

## Aggregate safety

Aggregate rewrites are especially sensitive to:

- empty collections;
- NULL values;
- duplicate rows;
- cardinality;
- authorization filtering.

Foundgine therefore models aggregate legality and provider capability separately from the rewrite itself.

`AggregateRewriteProof` combines the relevant equivalence, semantic legality, provider capability, and security checks before an aggregate rewrite is accepted.

## Provider-aware cost estimation

`IProviderCostEstimator` allows a provider to supply advisory cost estimates.

```text
logical rewrite candidates
          ↓
provider cost estimate
          ↓
candidate selection
          ↓
semantic/security proof
          ↓
accepted rewrite
```

Cost is advisory. A cheap estimate never overrides semantic correctness or security.

Cost estimates include provenance and freshness metadata so heuristic estimates are not mistaken for live database statistics.

## Mutation planning

Read and mutation planning use separate algebras.

Mutation concepts include:

```text
MutationPlan
  └── MutationOperation
       ├── Create
       ├── Update
       ├── Delete
       └── Upsert
```

Dependencies between operations are explicit.

This is important for nested writes where one generated key becomes an input to a later operation.

### Authorization

`MutationAuthorizer` applies semantic mutation authorization after structural planning and before provider compilation.

The planner itself should not invent application authorization.

## Provider independence

A planner can be used by:

- SQL;
- InMemory;
- future providers.

A provider should consume the logical plan and lower it into its own representation.

```text
Foundgine.Planning
        │
        ▼
ProviderPlanCompiler
   ┌────┼────┐
   ▼    ▼    ▼
 SQL  Memory Future
```

## Execution IR

The current architecture also has a canonical `ExecutionIR` boundary.

The logical plan can be lowered to execution IR before provider compilation:

```text
SemanticPlan
    ↓
ExecutionIR
    ↓
Provider compiler
```

This gives execution/security gates a stable representation without moving SQL into the planner.

## What does not belong here

Do not add:

- SQL strings;
- database table/column names;
- ADO.NET parameters;
- GraphQL AST nodes;
- database connection objects;
- LLM/tool definitions.

If a proposed planner feature is provider-specific, it probably belongs after this boundary.

## Related packages

- `Foundgine.Semantics` — source meaning and authorization.
- `Foundgine.Execution` — execution IR, provider contracts, evidence.
- `Foundgine.Sql` — SQL lowering and PostgreSQL physical execution.
- `Foundgine.InMemory` — provider-independence proof implementation.

## Design rule

Before adding a new logical operation, ask:

> **Does this describe a fundamentally different semantic execution topology?**

If not, it should probably be a clause, property, rewrite, or provider concern instead.
