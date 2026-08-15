# Derived correlation dependencies — PostgreSQL uses derived correlation dependencies

The canonical `ExecutionMutationIR` path now derives dependency edges from
`MutationFieldValue.Source` references.

```text
MutationFieldValue.Source
        ↓
DeriveDependencies()
        ↓
MutationDependency
        ↓
Postgres dependency levels
```

The legacy `ExecutionMutationIR.Dependencies` collection is retained temporarily
and is validated against the derived edges. A mismatch is a hard compiler error.

This is an intentional migration guard: it prevents two dependency sources from
silently diverging while existing planners/tests are migrated.

The PostgreSQL compiler's canonical `Compile(ExecutionMutationIR)` entry point
now uses the derived dependencies rather than trusting independently supplied
dependency metadata.

The next step is to remove `Dependencies` from the execution IR once all
producers construct field references correctly.
