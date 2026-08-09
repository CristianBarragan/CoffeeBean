[Home](../../README.md) → [Documentation](../README.md) → **Source Generators**

# Source Generators

> **Historical implementation + future direction.**

The repository previously contained a Graphgine source generator for mapping-derived metadata and execution artifacts. That implementation now lives under `archive/`.

The current active tree does not contain a source-generator project.

## Future role

Roslyn generation remains valuable, but its purpose has changed.

The future compiler should derive the application's semantic vocabulary:

```text
C# Domain
 ↓
Roslyn
 ↓
Semantic Domain Model
 ↓
Generated descriptors
```

Potential generated artifacts:

- stable entity IDs
- relationship descriptors
- search descriptors
- action descriptors
- policy metadata
- planner hints

## What the compiler should not do

It should not generate a fixed planner for natural-language requests.

The request is dynamic:

```text
User intent
 ↓
runtime resolution
 ↓
runtime planning
```

The compiler defines what the application permits; runtime decides which permitted operations satisfy the current intent.

See [Proof Milestones](../00-Direction/Milestones.md#milestone-10--compile-time-semantic-compiler).
