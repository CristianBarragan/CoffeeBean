# Mutation Correlation — Single Source of Truth

Dependency correlation consumer audit establishes the intended invariant:

> A mutation dependency is derived from a correlation reference.

The authoritative execution relationship is:

```text
source operation
    + source returned field
    + target operation
    + target field
            ↓
    MutationCorrelationReference
            ↓
    dependency graph
```

The dependency graph must not independently describe a different relationship.

## Why

Previously the transitional mutation model contained both:

```text
MutationValueReference
MutationDependency
```

which could disagree.

That creates an ambiguity in physical lowering.

The canonical model is now:

```text
MutationCorrelationReference
        ↓
MutationCorrelationGraph
        ↓
batch dependency levels
        ↓
PostgreSQL physical lowering
```

## Migration rule

Existing `MutationDependency` APIs may remain temporarily for compatibility, but
new compiler logic must derive dependencies from correlation references.

Once all consumers migrate, the duplicated dependency representation can be
removed.

## Multiple consumers

A source operation may have multiple correlation references:

```text
Create Customer
   ├── Account.customerId
   └── Address.customerId
```

The source operation remains one logical operation and the graph contains two
edges.

## Physical batching

Batch grouping must never create or destroy semantic dependency edges.
