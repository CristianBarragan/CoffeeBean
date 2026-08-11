# M16 — Mutations

M16 introduces the first provider-neutral mutation boundary.

Pipeline:

MutationIntent -> MutationPlanner -> MutationPlan -> provider execution

Supported operations:
- Create
- filtered Update
- filtered Delete

Safety rules:
- Update/Delete require a filter.
- Delete cannot contain field values.
- Create/Update require at least one field value.
- mutation filters may target fields of the mutated entity.
- relationship/aggregate mutation filters are deferred.

SQL compilation, upsert, generated identity, dependency ordering, and GraphQL mutation translation are intentionally not part of M16.
