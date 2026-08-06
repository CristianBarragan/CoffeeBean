[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **ADR Process**

# ADR Process

## Contents

- [When to write an ADR](#when-to-write-an-adr)
- [Format](#format)
- [Where ADRs live](#where-adrs-live)
- [Review](#review)

---

## When to write an ADR

Write an Architecture Decision Record before making a change that:

- Alters a dependency direction between layers (see [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md))
- Adds or changes a Foundation contract (see [Foundation → Contracts](../03-Foundation/Contracts.md))
- Adds a new execution provider or transport (see [Architecture → Vision](../02-Architecture/Vision.md#roadmap-by-phase))
- Changes how the source generator discovers or validates mappings

Small implementation details, bug fixes, and refactors that don't change a public contract
don't need one.

## Format

Follow the existing ADRs in [Reference → ADRs](../13-Reference/ADRs.md) as a template:
**Status**, **Context**, **Decision**, **Consequences** (split into Advantages and
Trade-offs). Keep each ADR focused on one decision.

## Where ADRs live

All accepted ADRs live in one place: [`docs/13-Reference/ADRs.md`](../13-Reference/ADRs.md),
numbered sequentially (`ADR-013`, `ADR-014`, ...). Don't create standalone ADR files
scattered across sections — a single, append-only list is what makes the decision history
searchable.

## Review

Open a pull request with the new ADR appended, following the
[review checklist](README.md#pull-requests). An ADR should be reviewed and merged (Status:
Accepted) before the change it describes is implemented, not after.

---

## Related Documentation

- [Reference → ADRs](../13-Reference/ADRs.md)
- [Contributing](README.md)
- [Architecture](../02-Architecture/README.md)

---

← Previous: [Testing](Testing.md)  |  Next: [Reference](../13-Reference/README.md) →
