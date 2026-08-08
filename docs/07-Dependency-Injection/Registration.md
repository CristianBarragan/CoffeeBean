[Home](../../README.md) → [Documentation](../README.md) → [Dependency Injection](README.md) → **Registration**

# Registration

## Contents

- [Composition Root](#composition-root)
- [Foundation Contracts](#foundation-contracts)
- [Generated Registration](#generated-registration)
- [Runtime Registration](#runtime-registration)
- [SQL Registration](#sql-registration)
- [GraphQL Registration](#graphql-registration)

---

## Composition Root

Each transport owns its own composition root.

Examples:

```
Foundgine.GraphQL

Foundgine.WebApi

Foundgine.Grpc
```

Each project registers Runtime plus generated services.

---

## Foundation Contracts

The Runtime depends on interfaces defined by Foundation.

Typical contracts include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer

ISqlDialect

IGraphStrategy
```

Generated implementations satisfy these interfaces.

---

## Generated Registration

The Generator should emit a registration extension.

Example:

```csharp
public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection
        AddGeneratedCoffeeBeanery(
            this IServiceCollection services)
    {
        services.AddSingleton<IMetadataProvider,
            GeneratedMetadataProvider>();

        services.AddSingleton<IPlannerRegistry,
            GeneratedPlannerRegistry>();

        return services;
    }
}
```

Generated code contains registrations—not application logic.

---

## Runtime Registration

Runtime exposes its own registration extension.

Example:

```csharp
public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection
        AddCoffeeBeaneryRuntime(
            this IServiceCollection services)
    {
        services.AddSingleton<IQueryExecutor,
            QueryExecutor>();

        services.AddSingleton<IMutationExecutor,
            MutationExecutor>();

        return services;
    }
}
```

Runtime never registers generated components.

---

## SQL Registration

SQL providers expose separate registration methods.

Example:

```csharp
services.AddPostgreSql();
```

Internally this registers:

- ISqlWriter
- ISqlReader
- ISqlDialect
- IGraphStrategy

Database providers remain modular.

---

## GraphQL Registration

GraphQL composes the complete framework.

Typical setup:

```csharp
services

    .AddCoffeeBeaneryRuntime()

    .AddGeneratedCoffeeBeanery()

    .AddPostgreSql()

    .AddCoffeeBeaneryGraphQL();
```

GraphQL becomes a thin adapter over Runtime.

---

---

## Related Documentation

- [Lifetimes](Lifetimes.md)
- [Getting Started → Configuration](../01-Getting-Started/Configuration.md)
- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)

---

← Previous: [Dependency Injection](README.md)  |  Next: [Lifetimes](Lifetimes.md) →
