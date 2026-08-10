# PostgreSQL and Apache AGE

> **Future / historical context — not an active provider claim.**

The current canonical provider proof is SQLite.

PostgreSQL and graph technologies may become execution targets later, but the active repository should not claim PostgreSQL/AGE support unless an active provider and E2E tests prove it.

The desired architecture is:

```text
Provider-neutral QueryPlan
        ↓
PostgreSQL provider
        ↓
PostgreSQL
```

or, for a graph execution target:

```text
Provider-neutral plan
        ↓
Graph provider
        ↓
graph engine
```

Graph semantics must not leak into the core simply because one provider uses a graph database.
