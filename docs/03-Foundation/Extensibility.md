[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Extensibility**

# Extensibility

## Contents

- [Philosophy](#philosophy)
- [Architectural Principle](#architectural-principle)
- [Extension Categories](#extension-categories)
- [Best Practices](#best-practices)

---

> Foundgine is designed to be extended through well-defined contracts rather than inheritance or runtime discovery. This document describes the framework's extensibility model and identifies the supported extension points.

---

## Philosophy

Foundgine follows the **Open/Closed Principle**.

The framework should be:

- Open for extension
- Closed for modification

Applications should be able to customize behavior without changing the Runtime.

---

## Architectural Principle

Every extension point lives behind a Foundation interface.

```
Application

↓

Custom Implementation

↓

Foundation Interface

↓

Runtime
```

Runtime never depends upon application code directly.

---

## Extension Categories

Foundgine exposes extension points in several areas:

```
Metadata

Planning

SQL

Materialization

Dematerialization

Graph

Interceptors

Dependency Injection

Transports
```

Each category has a clearly defined responsibility.

---

## Metadata Providers

Metadata is supplied through `IMetadataProvider`.

```csharp
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort storageEntityId);

    ModelMetadata GetModel(ushort modelId);

    JoinMetadata? GetJoin(
        ushort leftStorageEntity,
        ushort rightStorageEntity);

    GraphMetadata? GetGraph(ushort graphId);
}
```

Most applications use the generated implementation.

Advanced scenarios may provide custom metadata sources.

---

## Planner Registry

The planner registry maps models to generated planners.

Example contract:

```csharp
public interface IPlannerRegistry
{
    QueryPlanner GetQueryPlanner(ushort modelId);

    MutationPlanner GetMutationPlanner(ushort modelId);
}
```

Generated registries are the default implementation.

---

## SQL Dialects

SQL generation is intentionally database-independent.

A dialect implementation owns provider-specific syntax.

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteReturning(...);

    void WriteConflict(...);
}
```

Potential implementations:

- PostgreSQL
- SQL Server
- MySQL
- SQLite
- Oracle

---

## SQL Writers

Applications may replace SQL writers.

Example:

```csharp
ISqlWriter
```

Possible customizations:

- Multi-tenant SQL
- Audit SQL
- Soft-delete behavior
- Vendor-specific optimizations

---

## Materializers

Materializers convert rows into CLR objects.

```csharp
IEntityMaterializer
```

Generated implementations should satisfy most scenarios.

Custom materializers may support:

- Immutable records
- Custom collections
- Domain object construction

---

## Dematerializers

Dematerializers convert CLR objects into mutation values.

```csharp
IEntityDematerializer
```

Custom implementations may support:

- Domain events
- Change tracking
- Alternate serialization

---

## Graph Strategy

Graph support is isolated behind a strategy interface.

Example:

```csharp
IGraphStrategy
```

Possible implementations:

- Apache AGE
- Neo4j bridge
- Custom graph database
- No-op implementation

This isolates graph behavior from Runtime.

---

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

## Dependency Injection

Every generated component should be replaceable.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
                      GeneratedMetadataProvider>();
```

Applications may substitute:

```csharp
services.AddSingleton<IMetadataProvider,
                      CustomMetadataProvider>();
```

Runtime remains unchanged.

---

## Transport Extensions

Runtime is transport agnostic.

New transports can integrate by translating requests into immutable plans.

Potential transports include:

- GraphQL
- gRPC
- REST
- SignalR
- CLI
- Background workers

No Runtime changes should be required.

---

## Storage Providers

Although Foundgine currently targets PostgreSQL, the architecture supports additional storage engines.

Potential future providers:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

Each provider implements SQL abstractions while reusing the same planners and Runtime.

---

## Generator Extensions

The Mapping Generator can evolve through additional emitters.

Examples:

```
IdEmitter

MetadataEmitter

PlannerEmitter

MaterializerEmitter

InterceptorEmitter

DependencyInjectionEmitter
```

Future emitters can generate additional compile-time artifacts without affecting existing Runtime components.

---

## Best Practices

When extending Foundgine:

- Prefer interfaces over inheritance
- Preserve immutability
- Avoid reflection
- Respect project boundaries
- Keep generated code deterministic
- Register implementations through Dependency Injection

Extensions should integrate with the framework rather than bypass it.

---

## Summary

Foundgine is intentionally extensible through Foundation contracts.

By exposing clear interfaces for metadata, planning, SQL generation, materialization, graph strategies, and transports, the framework can evolve without compromising its core architecture of compile-time generation, immutable execution plans, and transport-independent Runtime.

---

## Related Documentation

- [Foundation → Contracts](Contracts.md)
- [Architecture → Principles](../02-Architecture/Principles.md)
- [Runtime → Events](../04-Runtime/Events.md)

---

← Previous: [Components](Components.md)  |  Next: [Runtime](../04-Runtime/README.md) →
