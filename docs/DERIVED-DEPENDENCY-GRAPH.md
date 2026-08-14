# Derived dependency graph — Derived Dependency Graph

The mutation execution model is now intended to have one source of truth:

```text
ExecutionMutationIR
└── Operations
     └── Fields
          └── correlation/source reference
```

The dependency DAG is a derived view:

```text
ExecutionMutationIR
        ↓
derive correlation references
        ↓
MutationCorrelationGraph
        ↓
dependency levels
```

`MutationDependency` must not remain an independently authored semantic fact.

## Migration rule

Any existing dependency collection on `ExecutionMutationIR` is transitional.
Consumers must derive dependencies from operation field references.

The final removal must happen only after all constructors, serializers,
providers, tests, and benchmark code have migrated.

## Correctness invariant

Two independent dependency representations must never be allowed to disagree.
