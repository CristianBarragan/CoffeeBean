# Changelog

## Current documentation realignment — August 2026

The documentation is aligned with the current active Foundgine repository and its proof-driven scope.

### Product

- Foundgine is consistently described as a .NET application-domain semantic and execution platform.
- GraphQL/Graphgine is historical and remains outside the active core.
- AI and MCP are positioned as outer integrations.

### Architecture

- `Foundgine.Semantic` is the active semantic layer and depends only on `Foundgine.Metadata`.
- `Foundgine.Planning` remains the single logical planner.
- The semantic layer is expected to translate into `QueryIntent`, not introduce a second planner hierarchy.
- Resolution and relationship traversal are explicitly distinguished: resolution identifies an entity; traversal can represent a collection.

### Proof status

The active proof includes:

- linear traversal;
- branching traversal;
- ugly physical schema;
- five-entity composite;
- repeated/self-joined entities;
- filtering, sorting and paging;
- create/update/delete mutation planning/execution;
- semantic resolution;
- structured read intent;
- semantic/read intent through real SQLite;
- composite semantic/read proof.

### Current next step

The remaining core gap is not another architecture layer. It is the reusable semantic → `QueryIntent` bridge, followed by collection-aware traversal and benchmark evidence.

### Scope discipline

Action/policy descriptors remain experimental/future-facing. MCP, full LLM intent extraction, Roslyn compilation, AOT and additional providers remain future directions unless active code and tests prove them.

### Accuracy

Future capabilities are explicitly labelled as planned rather than implemented.
