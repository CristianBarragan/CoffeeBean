# Foundgine documentation

This directory contains the **current** Foundgine documentation for the 1.1.7 release line.

The repository itself is the source of truth. Documentation describes implemented architecture and tested behavior; it does not use old release notes or historical benchmark snapshots as current product guidance.

## Read in order

This is the reading path — each page ends with a link to the next one, so you can also just start at [Getting started](GETTING-STARTED.md) and follow the links.

1. [Getting started](GETTING-STARTED.md) — build the repository and run the main sample.
2. [Why Foundgine](WHY-FOUNDGINE.md) — the problem, and the boundary it draws.
3. [Architecture](ARCHITECTURE.md) — the intent-to-provider pipeline.
4. [Metadata → Semantics](METADATA-TO-SEMANTICS.md) — what exists versus what it means.
5. [Mapping and connections](MAPPING.md) — how the semantic model attaches to the physical/EF model.
6. [Open Intent API](OPEN-INTENT-API.md) — typed, dynamic, and JSON ways to express intent.
7. [Authorization](AUTHORIZATION.md) — what a caller may exercise, and under what conditions.
8. [Security](SECURITY.md) — the untrusted-input boundary and fail-closed rules.
9. [Runtime](RUNTIME.md) — how a request actually moves through resolution, planning, and execution.
10. [AOT](AOT.md) — moving metadata discovery to compile time.
11. [Public API](PUBLIC-API.md) — the shape of the day-to-day application surface.
12. [AI agents](AI-AGENT.md) — exposing capabilities to agents and MCP without granting database authority.
13. [PostgreSQL E2E](POSTGRES-E2E.md) — the real database integration path.
14. [Testing](TESTING.md) — how the boundaries above are proven, not just asserted.
15. [Current status](CURRENT-STATUS.md) — what the active code and tests support today.
16. [Roadmap](ROADMAP.md) — what's next, and what's deliberately out of scope.
17. [Migration](MIGRATION.md) — moving code over from the archived V1/Graphgine projects.

Package-specific architecture and usage guidance lives in the `README.md` of every project under `src/`.

## Documentation rules

The documentation follows four rules:

1. **Current code wins.** If prose disagrees with source or tests, the source/tests are authoritative.
2. **Implemented and planned are separated.** A planned feature is not documented as a shipped capability.
3. **Transport and provider boundaries stay explicit.** GraphQL, MCP, JSON, AI, and SQL are adapters/providers around the semantic core.
4. **Historical material stays out of the active guide.** Release notes, old benchmark runs, and implementation diaries are not part of the current documentation set.

Package-specific architecture and usage guidance lives in the `README.md` of every project under `src/`.
