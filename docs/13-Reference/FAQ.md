[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **FAQ**

# FAQ

Extended architecture FAQ. For first-hour setup questions, see
[Getting Started → FAQ](../01-Getting-Started/FAQ.md).

## Contents

- [Why does Foundgine use Source Generators?](#why-does-coffeebeanery-use-source-generators)
- [Why not use reflection?](#why-not-use-reflection)
- [Why split Foundation from Runtime?](#why-split-foundation-from-runtime)
- [Why generate metadata?](#why-generate-metadata)
- [Why immutable metadata?](#why-immutable-metadata)
- [Why immutable execution plans?](#why-immutable-execution-plans)
- [Why separate Planning from SQL?](#why-separate-planning-from-sql)
- [Why generate materializers?](#why-generate-materializers)
- [Why not build SQL inside GraphQL?](#why-not-build-sql-inside-graphql)
- [Why support multiple transports?](#why-support-multiple-transports)
- [Why use Dependency Injection?](#why-use-dependency-injection)
- [Why avoid static generated classes?](#why-avoid-static-generated-classes)
- [Why does Runtime avoid Roslyn?](#why-does-runtime-avoid-roslyn)
- [Why prioritize Native AOT?](#why-prioritize-native-aot)
- [Can Foundgine support databases other than PostgreSQL?](#can-coffeebeanery-support-databases-other-than-postgresql)
- [Can Foundgine support transports other than GraphQL?](#can-coffeebeanery-support-transports-other-than-graphql)
- [Why generate identifiers?](#why-generate-identifiers)
- [What belongs in Foundation?](#what-belongs-in-foundation)
- [What belongs in Runtime?](#what-belongs-in-runtime)
- [What belongs in the Generator?](#what-belongs-in-the-generator)
- [What makes Foundgine different?](#what-makes-coffeebeanery-different)
- [Summary](#summary)

---

> This document answers the most common questions about Foundgine's architecture, design decisions, and development philosophy.

---

## Why does Foundgine use Source Generators?

Foundgine performs most framework analysis during compilation rather than runtime.

This includes:

- Metadata generation
- Relationship analysis
- Identifier allocation
- Materializer generation
- Planner generation
- Runtime registrations

This significantly reduces runtime work while improving startup performance and Native AOT compatibility.

---

## Why not use reflection?

Reflection introduces:

- Startup overhead
- Additional allocations
- Dynamic behavior
- Native AOT limitations
- Runtime uncertainty

Generated code provides the same information without requiring runtime discovery.

---

## Why split Foundation from Runtime?

Foundation defines contracts.

Runtime implements behavior.

Keeping them separate provides:

- Stable interfaces
- Better testing
- Dependency inversion
- Transport independence
- Cleaner project references

Foundation should never know Runtime exists.

---

## Why generate metadata?

Metadata rarely changes while an application is running.

Generating metadata once during compilation avoids repeated runtime analysis and enables immutable, singleton metadata objects.

---

## Why immutable metadata?

Immutable metadata is:

- Thread-safe
- Reusable
- Predictable
- Easy to cache
- Easy to test

Runtime never needs to modify metadata.

---

## Why immutable execution plans?

Planning determines *what* should happen.

Execution determines *when* it happens.

Separating these responsibilities simplifies Runtime and improves determinism.

---

## Why separate Planning from SQL?

Planning understands application semantics.

SQL understands database syntax.

Keeping them independent allows:

- Better testing
- Multiple SQL dialects
- Cleaner architecture
- Simpler SQL writers

---

## Why generate materializers?

Generated materializers:

- Avoid reflection
- Read values by ordinal
- Reduce allocations
- Improve performance
- Support Native AOT

Materialization becomes simple generated code.

---

## Why not build SQL inside GraphQL?

GraphQL is a transport.

Its responsibilities are:

- Schema
- Resolvers
- Request parsing

SQL belongs to the SQL project.

Keeping transport and execution separate makes Runtime reusable.

---

## Why support multiple transports?

The same execution engine should support:

- GraphQL
- gRPC
- REST
- CLI
- Background workers

Only the request translation changes.

Execution remains identical.

---

## Why use Dependency Injection?

Dependency Injection allows Runtime to depend upon interfaces rather than generated implementations.

For example:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

This improves testing and extensibility.

---

## Why avoid static generated classes?

Static classes tightly couple Runtime to generated code.

Generated implementations registered through interfaces allow:

- Mocking
- Replacement
- Testing
- Multiple implementations

---

## Why does Runtime avoid Roslyn?

Roslyn is a compile-time technology.

Runtime should execute plans—not analyze source code.

Keeping Roslyn isolated within the Generator reduces complexity and improves portability.

---

## Why prioritize Native AOT?

Native AOT aligns naturally with Foundgine's architecture.

Compile-time generation eliminates the need for:

- Reflection
- Dynamic proxies
- Runtime code generation
- Expression compilation

The resulting framework performs well in both JIT and AOT environments.

---

## Can Foundgine support databases other than PostgreSQL?

Yes.

Planning is database-independent.

Only SQL serialization changes.

Future providers may include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

---

## Can Foundgine support transports other than GraphQL?

Yes.

Runtime is transport agnostic.

Future transports may include:

- gRPC
- REST
- SignalR
- CLI
- Batch processing

---

## Why generate identifiers?

Stable generated identifiers provide:

- Fast lookups
- Array indexing
- Deterministic output
- Smaller runtime overhead

Identifiers are allocated during compilation.

---

## What belongs in Foundation?

Foundation contains:

- Metadata
- Interfaces
- Planning primitives
- Runtime primitives
- Identifiers

It intentionally excludes:

- Runtime
- SQL
- Roslyn
- GraphQL
- Generated code

---

## What belongs in Runtime?

Runtime owns:

- Query execution
- Mutation execution
- Transaction coordination
- Materialization orchestration
- Execution pipelines

Runtime should never:

- Discover metadata
- Parse attributes
- Generate SQL
- Inspect CLR models

---

## What belongs in the Generator?

The Generator performs compile-time work:

- Model discovery
- Validation
- Relationship resolution
- Metadata generation
- Materializer generation
- Planner generation
- Runtime registration generation

Generated code becomes Runtime's input.

---

## What makes Foundgine different?

Foundgine differs from many data frameworks by emphasizing:

- Compile-time analysis
- Immutable metadata
- Immutable execution plans
- Source generation
- Transport independence
- Dependency inversion
- Native AOT compatibility

The framework is designed so Runtime executes precomputed artifacts rather than discovering application structure during execution.

---

## Summary

Foundgine's design choices consistently favor compile-time computation, immutable models, clear architectural boundaries, and reusable execution components.

Understanding these principles makes the rest of the framework significantly easier to understand and extend.

---

## Related Documentation

- [ADRs](ADRs.md)
- [Glossary](Glossary.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [ADRs](ADRs.md)  |  Next: [Glossary](Glossary.md) →
