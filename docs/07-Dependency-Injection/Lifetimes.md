[Home](../../README.md) → [Documentation](../README.md) → [Dependency Injection](README.md) → **Lifetimes**

# Lifetimes

## Contents

- [Lifetime Guidelines](#lifetime-guidelines)
- [Replacing Implementations](#replacing-implementations)
- [Avoid Service Location](#avoid-service-location)
- [Testing](#testing)

---

## Lifetime Guidelines

Recommended service lifetimes:

| Component | Lifetime |
|-----------|----------|
| Metadata Provider | Singleton |
| Planner Registry | Singleton |
| SQL Dialect | Singleton |
| Graph Strategy | Singleton |
| Query Executor | Singleton |
| Mutation Executor | Singleton |
| Materializers | Singleton |
| Dematerializers | Singleton |

Execution state belongs in scoped execution contexts rather than service instances.

---

## Replacing Implementations

Applications may replace any generated implementation.

Example:

```csharp
services.Replace(

    ServiceDescriptor.Singleton<
        IMetadataProvider,
        CustomMetadataProvider>());
```

Runtime requires no modification.

---

## Testing

Dependency Injection makes testing straightforward.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
    TestMetadataProvider>();
```

Unit tests can replace:

- Metadata
- Planner registry
- SQL dialect
- Graph strategy

without changing Runtime.

---

## Avoid Service Location

Runtime should receive dependencies through constructors.

Preferred:

```csharp
public QueryExecutor(
    IMetadataProvider metadata,
    ISqlWriter writer)
{
}
```

Avoid resolving services directly from `IServiceProvider`.

Constructor injection makes dependencies explicit and easier to test.

---

---

## Related Documentation

- [Registration](Registration.md)
- [Contributing → Testing](../12-Contributing/Testing.md)

---

← Previous: [Registration](Registration.md)  |  Next: [Persistence](../08-Persistence/README.md) →
