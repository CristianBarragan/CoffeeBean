[Home](../../README.md) → [Documentation](../README.md) → [Performance](README.md) → **Native AOT**

# Native AOT

## Contents

- [Why Native AOT?](#why-native-aot)
- [Design Principles](#design-principles)
- [Dynamic Features to Avoid](#dynamic-features-to-avoid)
- [Testing Native AOT](#testing-native-aot)
- [Performance Benefits](#performance-benefits)

---

## Why Native AOT?

Native AOT provides:

- Faster startup
- Lower memory usage
- Smaller deployment footprint
- Better container performance
- Reduced cold-start latency
- Improved cloud scalability

Supporting Native AOT also encourages better architectural discipline.

---

## Design Principles

CoffeeBeanery achieves Native AOT compatibility by avoiding runtime features that require dynamic analysis.

Key principles include:

- No runtime reflection
- No runtime code generation
- No expression compilation
- No dynamic proxy generation
- No runtime metadata discovery

Everything required for execution is generated during compilation.

---

## Dynamic Features to Avoid

Avoid introducing:

- Reflection
- DynamicMethod
- Reflection.Emit
- Expression.Compile()
- Runtime IL generation
- Dynamic proxies
- Runtime assembly scanning

These features either reduce AOT compatibility or require additional configuration.

---

## Collections

Prefer static, immutable collections.

Examples:

```csharp
ImmutableArray<T>

ImmutableDictionary<TKey, TValue>
```

Generated metadata should be initialized once and reused for the application's lifetime.

---

## Generic Code

Prefer closed generic registrations where practical.

Avoid runtime generic construction using reflection.

Generated registries should reference concrete implementations directly.

---

## Serialization

Where serialization is required, prefer source-generated serializers.

Example:

```csharp
[JsonSerializable(typeof(Customer))]
internal partial class CoffeeBeaneryJsonContext
    : JsonSerializerContext
{
}
```

Avoid reflection-based serializers.

---

## SQL

SQL generation should remain purely deterministic.

SQL writers should consume immutable execution plans without inspecting CLR types.

This naturally aligns with Native AOT constraints.

---

## Testing Native AOT

Native AOT should be validated continuously.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Query execution
- Mutation execution
- Materialization
- Metadata resolution

These tests help prevent accidental introduction of unsupported runtime features.

---

## Performance Benefits

Designing for Native AOT also improves traditional JIT execution.

Benefits include:

- Fewer allocations
- Reduced startup work
- Simpler execution paths
- Better cache locality
- More predictable performance

Compile-time optimization benefits every deployment model.

---

---

## Related Documentation

- [Benchmarks](Benchmarks.md)
- [Source Generators](../06-Source-Generators/README.md)
- [Foundation → Components](../03-Foundation/Components.md)

---

← Previous: [Performance](README.md)  |  Next: [Benchmarks](Benchmarks.md) →
