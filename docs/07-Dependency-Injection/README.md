# Dependency Injection

Dependency injection is the composition mechanism, not the semantic model.

## Rule

DI should answer:

> Which implementation satisfies this contract?

It should not decide:

- what an entity means
- how an intent is resolved
- whether an action is authorized
- how a query is planned
- how evidence is produced

## Current composition

The active sample constructs the necessary components directly so the E2E remains obvious.

As the runtime grows, DI can become the composition root for:

```text
Metadata provider
Planner
Resolver
Policy evaluator
Execution provider
Verifier
Evidence sink
```

## Target composition

```text
Application
    ↓
DI composition root
    ↓
Foundgine semantic/execution services
    ↓
provider adapters
```

Do not introduce registration APIs until the underlying contracts are proven.
