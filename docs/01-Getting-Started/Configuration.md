# Configuration

Foundgine currently prefers explicit composition over a large framework registration layer.

## Core configuration

An application needs to supply:

```text
MetadataRegistry
JoinGraph
SemanticModel
CandidateSource
QueryPlanner
ExecutionProvider
```

The canonical Banking sample constructs these explicitly so the proof remains visible.

## Future DI integration

An application may eventually register:

```text
SemanticModel
CandidateSource
Resolver
Planning services
Execution providers
Policy services
Evidence services
```

through its existing DI container.

This should remain composition, not a second semantic configuration language.

## Configuration principle

> Configure what Foundgine cannot infer; do not configure facts already represented in metadata.
