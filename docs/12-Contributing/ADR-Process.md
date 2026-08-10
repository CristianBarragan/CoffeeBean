# ADR Process

Write an ADR before making a change that materially alters:

- project dependency direction;
- semantic/execution boundaries;
- provider contracts;
- metadata identity;
- mutation safety;
- public product scope.

## ADR format

```text
Context
Decision
Alternatives
Consequences
Status
```

## Particularly important decisions

Changes involving these should normally have an ADR:

```text
Semantic → Planning bridge
Action execution model
Policy model
MCP boundary
Roslyn compiler boundary
Provider contract changes
```

The objective is to preserve architectural intent as the repository grows.
