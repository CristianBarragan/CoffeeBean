# Dependency Injection

DI is a composition mechanism, not the semantic model.

The current Banking sample deliberately composes services explicitly so the E2E proof is easy to follow.

## Potential application composition

```text
SemanticModel
CandidateSource
Resolver
Planning services
Execution provider
Policy
Evidence
```

These can be registered with the application's existing DI container.

Foundgine should not introduce a container of its own.
