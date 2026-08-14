# PostgreSQL correlation validation — PostgreSQL Correlation Validation

The actual `PostgresBatchedMutationCompiler` now validates generated-value
correlation before SQL generation.

Validated conditions include:

- source operation exists;
- source operation is not the same operation;
- source dependency level precedes the target;
- source field exists on the source entity;
- source field has a physical column;
- a matching dependency edge exists for the reference;
- the source operation belongs to its expected physical group;
- DELETE cannot act as a generated-value correlation source.

This is intentionally a compiler guard, not a SQL/runtime workaround.

The provider may still reject a batch as unsupported through `NotSupportedException`
and allow the existing sequential fallback.

## Important finding

Correlation is represented twice in the current transitional model:

1. `MutationFieldValue.Source`
2. `MutationDependency`

PostgreSQL correlation validation treats disagreement between these representations as invalid rather
than choosing one silently.

The next step should consolidate that duplication in the execution representation,
not add another parallel correlation mechanism.
