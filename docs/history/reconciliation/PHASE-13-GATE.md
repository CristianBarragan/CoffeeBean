# Phase 13 Gate

## GO only when

1. The full solution builds.
2. Unit and integration tests pass.
3. Security/fuzz invariants pass.
4. MCP read and mutation flows pass end-to-end.
5. Duplicate public concepts have been reconciled.
6. Core dependency boundaries are clean.

## STOP if

- a second authorization path appears;
- MCP becomes a semantic authority;
- a provider bypasses the semantic plan;
- approval is treated as authorization;
- multiple receipt/evidence contracts become canonical;
- a new feature requires a parallel semantic model.
