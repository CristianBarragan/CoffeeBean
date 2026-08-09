> **Historical note:** This page describes the earlier GraphQL/source-generation architecture. The current Foundgine direction is documented in [Direction](../../00-Direction/README.md) and [Current Status](../../CURRENT-STATUS.md). Historical implementation is under `archive/`.

[Home](../../README.md) → [Documentation](../README.md) → [Source Generators](README.md) → **Diagnostics**

# Diagnostics

## Contents

- [Diagnostic Codes](#diagnostic-codes)
- [Known Risk Areas](#known-risk-areas)
- [Deterministic Output](#deterministic-output)
- [Testing](#testing)

---

## Diagnostic Codes

| Id | Mirrors (old runtime behavior) | Severity |
|---|---|---|
| CBMAP001 | `NodeBuilder` "WARNING: ... is type-incompatible with ..." | Warning |
| CBMAP002 | `NodeBuilder` "WARNING: ... has no matching property..." | Warning |
| CBMAP003 | `NodeBuilder.BuildEntityChildren` ambiguous-navigation exception | **Error** |
| CBMAP004 | (new) navigation-shaped property with no resolvable FK by convention | **Error** |
| CBMAP005 | (new) unsupported `BuildMap()` statement shape | **Error** |

CBMAP003 replaces a runtime `InvalidOperationException` for ambiguous navigations with a
build-time error — resolve it with a `ModelToEntity` alias entry, or the
`[EntityForeignKey]` escape hatch. See
[Mapping Generator → Ambiguous navigation handling](Mapping-Generator.md#ambiguous-navigation-handling)
for the full pattern.

## Known Risk Areas


> The Diagnostics subsystem is responsible for identifying architectural, modeling, and configuration issues during compilation rather than execution. Instead of allowing invalid applications to fail at runtime, Foundgine reports deterministic compiler diagnostics with actionable guidance, enabling developers to correct problems before the application is ever executed.

Diagnostics are part of the framework.

They are not an afterthought.

---

## Philosophy

Diagnostics follow one rule:

> **Every preventable runtime error should become a compile-time diagnostic.**

Compilation is the best opportunity to improve developer experience.

---

## Why Diagnostics?

Without diagnostics:

```
Compile

↓

Run

↓

Exception

↓

Debug
```

With diagnostics:

```
Compile

↓

Diagnostic

↓

Fix

↓

Run
```

Failures move left.

---

## High-Level Architecture

```
Source Code

↓

Parser

↓

Validation

↓

Diagnostics

↓

Generation
```

Invalid models never reach code generation.

---

## Responsibilities

The diagnostics subsystem is responsible for:

- Model validation
- Architecture validation
- Provider compatibility
- Metadata validation
- Graph validation
- Relationship validation
- Incremental diagnostics

Diagnostics never modify generated output.

---

## Diagnostic Lifecycle

Every diagnostic follows the same lifecycle.

```
Source

↓

Validation

↓

Diagnostic

↓

IDE

↓

Developer
```

Generation continues whenever possible.

---

## Diagnostic Categories

Diagnostics should be grouped by concern.

Examples:

```
Architecture

Metadata

Relationships

Planning

Providers

Generation

Performance
```

Each category should have a distinct identifier range.

---

## Identifier Convention

Diagnostic identifiers should remain stable.

Example:

```
CB1000

Architecture

CB2000

Metadata

CB3000

Relationships

CB4000

Planning

CB5000

Providers

CB9000

Internal Generator
```

Stable identifiers improve documentation and troubleshooting.

---

## Severity Levels

Diagnostics should clearly communicate severity.

```
Info

↓

Warning

↓

Error
```

Errors prevent generation.

Warnings allow generation.

---

## Error Philosophy

Errors indicate invalid applications.

Examples:

- Missing primary key
- Duplicate entity
- Circular dependency
- Invalid graph
- Unsupported mapping

Applications should not compile with structural errors.

---

## Warning Philosophy

Warnings indicate questionable designs.

Examples:

- Unused entity
- Redundant relationship
- Large projection
- Missing index recommendation
- Inefficient graph traversal

Warnings educate developers.

---

## Informational Diagnostics

Information diagnostics improve visibility.

Examples:

- Generated entity count
- Metadata statistics
- Incremental cache usage
- Optimization suggestions

Informational diagnostics should never block compilation.

---

## Validation Stages

Diagnostics may originate from multiple stages.

```
Syntax

↓

Semantic

↓

Model

↓

Metadata

↓

Planning
```

Each stage validates only its own responsibilities.

---

## Syntax Diagnostics

Examples include:

- Missing attributes
- Invalid declarations
- Unsupported modifiers

Syntax diagnostics occur before semantic analysis.

---

## Semantic Diagnostics

Examples:

- Unknown types
- Accessibility issues
- Generic misuse
- Invalid inheritance

Semantic analysis resolves compiler symbols.

---

## Model Diagnostics

Model validation includes:

- Duplicate entities
- Missing identifiers
- Invalid relationships
- Unsupported property types

Internal models should always be valid after this stage.

---

## Metadata Diagnostics

Metadata validation includes:

- Duplicate IDs
- Missing columns
- Invalid joins
- Graph inconsistencies

Runtime assumes metadata correctness.

---

## Planning Diagnostics

Planning validation includes:

- Cycles
- Invalid projections
- Ambiguous joins
- Unsupported filters

Invalid plans should never be generated.

---

## Provider Diagnostics

Providers may report compatibility issues.

Examples:

```
JSON not supported

Recursive CTE unavailable

Unsupported UPSERT strategy
```

Provider diagnostics should remain compile-time whenever possible.

---

## Analyzer Architecture

Analyzers should remain independent from generation.

Recommended structure:

```
Syntax Analyzer

Semantic Analyzer

Architecture Analyzer

Performance Analyzer

Provider Analyzer
```

Each analyzer owns one responsibility.

---

## Code Fixes

Many diagnostics should provide automatic fixes.

Examples:

```
Missing Attribute

↓

Add Attribute
```

```
Duplicate Identifier

↓

Generate New Identifier
```

Code fixes significantly improve developer experience.

---

## Diagnostic Messages

Messages should answer three questions:

1. What is wrong?
2. Why is it wrong?
3. How do I fix it?

Avoid vague diagnostics.

---

## Example Diagnostic

```
CB2004

Duplicate entity identifier.

The entity 'Customer' shares an identifier with
'Supplier'.

Assign unique identifiers or allow automatic
allocation.
```

The fix should be obvious.

---

## Diagnostic Location

Diagnostics should appear at the most relevant location.

Prefer:

```
Entity Declaration
```

Instead of:

```
Generated Code
```

Developers should never debug generated files.

---

## Incremental Diagnostics

Incremental generators should invalidate only affected diagnostics.

Changing:

```
Customer.cs
```

should not recompute diagnostics for unrelated entities.

---

## Performance Diagnostics

Future analyzers may detect:

- N+1 patterns
- Large projections
- Excessive joins
- Redundant graph traversals

Performance guidance belongs in the IDE.

---

## Architecture Diagnostics

Architectural analyzers may validate:

- Dependency direction
- Layer violations
- Provider boundaries
- Runtime dependencies

This helps preserve long-term architecture.

---

## Snapshot Testing

Diagnostics should be snapshot tested.

```
Input

↓

Diagnostics

↓

Snapshot
```

Changes become immediately visible during review.

---

## Documentation

Every diagnostic should have documentation.

Example:

```
CB3007

Relationship Cycle

Description

Example

Resolution

Related Diagnostics
```

Documentation should remain versioned.

---

## IDE Experience

Diagnostics should integrate naturally with:

- Visual Studio
- Rider
- VS Code

Developers should receive feedback while typing.

---

## Thread Safety

Analyzers should remain stateless.

All state should remain local to analysis.

Shared mutable state should be avoided.

---

## Native AOT

Diagnostics exist only during compilation.

They contribute nothing to runtime size or execution cost.

---

## Future Evolution

Potential future analyzers include:

- Security analyzer
- Authorization analyzer
- Migration analyzer
- SQL analyzer
- Query analyzer
- Graph optimization analyzer

Each analyzer should remain modular.

---

## Diagnostic Checklist

Before adding a new diagnostic, ask:

- Is this actionable?
- Can it be detected during compilation?
- Does it explain the fix?
- Is the identifier stable?
- Can it provide a code fix?
- Can it be independently tested?

If not, reconsider the design.

---

## Relationship to the Framework

Diagnostics surround the entire compile-time pipeline.

```
Source Code

↓

Analysis

↓

Diagnostics

↓

Generation

↓

Runtime
```

They improve the framework without increasing runtime complexity.

---

## Summary

The Diagnostics & Analyzer Architecture transforms structural, architectural, provider, and planning errors into clear compile-time diagnostics, allowing developers to correct issues before execution begins.

By combining incremental analyzers, deterministic validation, stable diagnostic identifiers, actionable messages, IDE integration, and optional code fixes, Foundgine delivers a modern developer experience while preserving a lightweight Runtime and strengthening the architectural integrity of the framework.

The generator's own README additionally flags these concrete risk areas for the first real
build against your mapping code:

- **`MappingClassParser`** only understands the exact statement shapes used in the sample's
  `ProductMapping.BuildMap()`. Any other shape (loops, conditionals, helper method calls)
  hits `CBMAP005` and needs the parser extended.
- **Enum dictionary parsing** is the most speculative part of the parser — it pattern-matches
  `{ Enum.Value.ToString(), (int)Enum.Value }` collection-initializer entries syntactically.
- **`EntityNavigationConvention`**'s principal-key convention (`"{RelatedType.Name}Key"`) is
  an assumption based on the sample mapping and may need adjusting for your schema.

## Deterministic Output

Generated output is deterministic — the same mapping input always produces the same
generated source, which matters for incremental build performance and for reviewable diffs
in generated code. See [Pipeline Stages](Pipeline-Stages.md#incremental-boundaries) for how
incremental generation scopes re-computation.

## Testing

See [Contributing → Testing](../12-Contributing/Testing.md) for the layered testing strategy
(parser tests, validation tests, identifier tests, snapshot tests) the generator is expected
to carry.

---

## Related Documentation

- [Mapping Generator](Mapping-Generator.md)
- [Pipeline Stages](Pipeline-Stages.md)
- [Contributing → Testing](../12-Contributing/Testing.md)

---

← Previous: [Mapping Generator](Mapping-Generator.md)  |  Next: [Pipeline Stages](Pipeline-Stages.md) →
