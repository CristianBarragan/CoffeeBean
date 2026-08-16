# M18.11 — Join Ordering / Multi-Relationship Planning

M18.11 adds a conservative relationship join-ordering hint to the semantic planner.

## Goal

Allow the planner to identify a preferred execution order for sibling relationship traversals without changing the logical child order, result shaping, authorization predicates, or relationship topology.

## New capability

`RelationshipJoinOrderingRule` (`relationship.join.order`) assigns a deterministic `TraversalOrder` to sibling relationship traversals when cardinality metadata is known.

The heuristic prefers traversals with stronger local selectivity signals:

- explicit filters
- relationship/aggregate filters
- limits
- one-cardinality relationships

Ties are resolved deterministically by relationship identifier and original position.

## Semantic boundary

`TraversalOrder` is physical planning metadata. It is intentionally excluded from the semantic-equivalence fingerprint but included in the execution/plan fingerprint.

Therefore:

```text
logical meaning: unchanged
physical strategy: may change
```

Providers must preserve observable result semantics when applying the hint.

## Security boundary

The rule cannot modify:

- authorization predicates
- tenant context
- relationship identity
- relationship cardinality
- filters
- pagination
- ordering
- requested fields

The existing security-preservation proof remains mandatory.

## Provider-aware cost

The SQL cost estimator now accounts for `TraversalOrder` through a conservative traversal-cost discount. This is heuristic evidence only. It cannot make an invalid semantic or security rewrite executable.

## Deliberate limitation

M18.11 does not reorder the semantic `Children` collection and does not claim to perform arbitrary SQL join reordering. The physical provider remains responsible for translating the traversal hint into a safe execution strategy.

## Proof chain

```text
relationship graph
    ↓
cardinality
    ↓
selectivity heuristic
    ↓
TraversalOrder
    ↓
semantic equivalence
    ↓
security preservation
    ↓
provider cost
    ↓
physical execution
```

## What this proves

- sibling relationship traversals can carry deterministic physical ordering metadata
- logical result shape remains unchanged by the rule
- provider cost estimation can account for the hint
- security and semantic proof gates remain mandatory

## What this does not prove

- optimal join ordering
- database-specific join plans
- accurate cardinality statistics
- PostgreSQL planner equivalence
- arbitrary relationship reordering
- improved wall-clock performance without provider execution benchmarks
