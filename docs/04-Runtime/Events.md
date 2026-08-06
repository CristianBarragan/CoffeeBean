[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Events**

# Events

## Contents

- [Current State](#current-state)
- [Interceptors (extension point)](#interceptors-extension-point)
- [Diagnostics and Logging](#diagnostics-and-logging)
- [What's Not Built Yet](#whats-not-built-yet)

---

## Current State

Coffee Beanery does not yet have a first-class eventing or pub/sub system. What exists
today is narrower: **interceptors** as an extension point in the execution pipeline, and
structured logging/diagnostics hooks at the runtime level.

## Interceptors (extension point)

## Interceptors

Interceptors provide lifecycle hooks.

Typical events include:

```
Before Planning

After Planning

Before SQL

After SQL

Before Execution

After Execution

Before Materialization

After Materialization
```

Interceptors should observe or augment behavior rather than replace core execution.

---

## Diagnostics and Logging

Runtime emits structured diagnostics and logging around each pipeline stage — see [Execution](Execution.md#error-handling) for the error-handling model those hooks feed into.

## What's Not Built Yet

A first-class domain-event or outbox-style eventing model — the kind that would let a
mutation publish an event a Kafka or Temporal provider could consume — is tracked as part of
the [future phases](../02-Architecture/Vision.md#roadmap-by-phase) (additional infrastructure
providers), not as something available today. If you need this now, the supported extension
point is an interceptor, not an event bus.

---

## Related Documentation

- [Foundation → Extensibility](../03-Foundation/Extensibility.md)
- [Architecture → Vision](../02-Architecture/Vision.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)

---

← Previous: [Mutations](Mutations.md)  |  Next: [GraphQL](../05-GraphQL/README.md) →
