# Source Generators

> **Historical implementation / future direction.**

The previous Graphgine repository contained a substantial Roslyn source-generator implementation.

That implementation is archived.

The active Foundgine tree does not currently contain a source-generator project.

## Future role

Roslyn remains useful as a future **domain compiler**.

The compiler could generate:

```text
Entity descriptors
Relationship descriptors
Search descriptors
Action descriptors
Policy metadata
Stable identifiers
Planner hints
```

The compiler should not generate a fixed planner for unknown future natural-language requests.

The runtime still needs to perform:

```text
intent
 → resolution
 → planning
```

dynamically.

## Rule

Do not revive the old generator merely to recreate configuration that the current metadata model can already infer.
