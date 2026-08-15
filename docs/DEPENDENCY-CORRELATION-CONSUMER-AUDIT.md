# Dependency correlation consumer audit

Introduces `MutationCorrelationReference` and `MutationCorrelationGraph` as the
canonical correlation/dependency model.

Existing duplicate dependency structures are intentionally retained as
compatibility surfaces in this stage.

The next compiler migration should replace reads of independent
`MutationDependency` data with the derived graph, then delete the duplicate
source once all providers and tests have migrated.
