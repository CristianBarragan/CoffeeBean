# M8 — Query Controls

M8 selectively ports the proven archive capabilities for scalar filtering, ordering, and basic pagination.

Flow:

SemanticRequest
  -> Resolve
  -> Authorize
  -> ExecutionPlan
  -> parameterized SQL
  -> SQLite

Ported:
- eq / neq / in filters
- and / or filter groups
- root scalar ordering
- first / skip / offset -> LIMIT/OFFSET
- SQL command parameters

Not ported yet:
- navigation / collection filters
- aliases / result shaping
- Relay cursor pagination (`after`/`before`)
- mixed-direction keyset predicates
- mutations

The archive's `WhereCompiler`, `OrderCompiler`, and SQL writers were used as implementation evidence. Their provider-specific and runtime-specific structures were not copied into the new semantic contracts.
