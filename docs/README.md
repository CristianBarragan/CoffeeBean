# Foundgine documentation

This directory contains the **current** Foundgine documentation for the 1.1.7 release line.

The repository itself is the source of truth. Documentation describes implemented architecture and tested behavior; it does not use old release notes or historical benchmark snapshots as current product guidance.

## Start here

1. [Getting started](GETTING-STARTED.md) — build the repository and run the main sample.
2. [Why Foundgine](WHY-FOUNDGINE.md) — understand the problem and product boundary.
3. [Architecture](ARCHITECTURE.md) — understand the semantic-to-provider pipeline.
4. [Open Intent API](OPEN-INTENT-API.md) — understand typed, dynamic, JSON, MCP, and agent-facing intent.
5. [Authorization](AUTHORIZATION.md) — understand semantic authorization and conditional policies.
6. [Security](SECURITY.md) — understand the security boundaries and fail-closed rules.

## Build the mental model

- [Metadata → Semantics](METADATA-TO-SEMANTICS.md)
- [Mapping and Connections](MAPPING.md)
- [AOT](AOT.md)
- [Runtime](RUNTIME.md)
- [Public API](PUBLIC-API.md)
- [AI agents](AI-AGENT.md)

## Provider and testing documentation

- [PostgreSQL E2E](POSTGRES-E2E.md)
- [Testing](TESTING.md)

## Project direction

- [Current status](CURRENT-STATUS.md)
- [Roadmap](ROADMAP.md)
- [Migration](MIGRATION.md)

## Documentation rules

The documentation follows four rules:

1. **Current code wins.** If prose disagrees with source or tests, the source/tests are authoritative.
2. **Implemented and planned are separated.** A planned feature is not documented as a shipped capability.
3. **Transport and provider boundaries stay explicit.** GraphQL, MCP, JSON, AI, and SQL are adapters/providers around the semantic core.
4. **Historical material stays out of the active guide.** Release notes, old benchmark runs, and implementation diaries are not part of the current documentation set.

Package-specific architecture and usage guidance lives in the `README.md` of every project under `src/`.
