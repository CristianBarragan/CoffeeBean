# Architecture Decision Records

This page records the active architectural decisions.

## ADR-001 — Domain semantics are protocol-neutral

**Decision:** semantic descriptors live in `Foundgine.Semantic` and do not reference GraphQL, SQL or an LLM.

**Reason:** the same domain semantics must be usable by multiple clients.

## ADR-002 — Structured intent is the Foundgine AI boundary

**Decision:** LLMs produce structured intent; Foundgine resolves and executes it.

**Reason:** keeps language reasoning separate from deterministic execution.

## ADR-003 — Metadata remains the execution source of truth

**Decision:** planners derive joins and physical details from metadata.

**Reason:** prevents domain-specific storage assumptions from leaking into generic planning.

## ADR-004 — Providers are adapters

**Decision:** SQL and future providers consume provider-neutral plans.

**Reason:** planning should not become a database dialect.

## ADR-005 — Ambiguity is a first-class result

**Decision:** resolution returns `Ambiguous` instead of guessing.

**Reason:** AI-facing infrastructure must fail safely.

## ADR-006 — Semantic configuration should be minimal

**Decision:** infer identity, fields and relationships where metadata already knows them.

**Reason:** avoid maintaining two domain descriptions.

## ADR-007 — Vertical proof before platform expansion

**Decision:** complete real E2E slices before adding transports/providers/frameworks.

**Reason:** prevents architectural overgrowth without product evidence.

## ADR-008 — Historical GraphQL code remains archived

**Decision:** Graphgine/GraphQL implementation stays under `archive/`.

**Reason:** it is useful history but is not the current product boundary.

New decisions should be added using [ADR Process](../12-Contributing/ADR-Process.md).
