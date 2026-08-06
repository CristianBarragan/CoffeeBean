[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **Testing**

# Testing

## Contents

- [Philosophy](#philosophy)
- [Testing Pyramid](#testing-pyramid)
- [Generator Tests](#generator-tests)
- [Runtime Tests](#runtime-tests)
- [Native AOT Tests](#native-aot-tests)
- [Continuous Integration](#continuous-integration)

---

## Philosophy

Testing should mirror the architecture.

```
Foundation

↓

Generator

↓

Runtime

↓

SQL

↓

Transport
```

Every layer has its own responsibilities and should be tested independently.

Avoid relying solely on end-to-end integration tests.

---

## Testing Pyramid

```
               End-to-End
            Integration Tests
             Snapshot Tests
               Unit Tests
```

The majority of tests should be unit tests.

Integration tests validate interactions between components.

Snapshot tests validate generated code.

---

## Generator Tests

The Generator requires the largest test surface.

Recommended categories:

```
Parser Tests

↓

Validation Tests

↓

Relationship Tests

↓

Identifier Allocation Tests

↓

Metadata Generation Tests

↓

Snapshot Tests
```

Each stage should be tested independently.

---

## Parser Tests

Parser tests verify discovery of application models.

Example scenarios:

- Entity detection
- Property discovery
- Graph discovery
- Join discovery
- Lookup discovery

Parser tests should isolate Roslyn analysis from code generation.

---

## Runtime Tests

Runtime tests verify execution behavior independently of SQL.

Examples:

- Query execution
- Mutation execution
- Dependency ordering
- Generated value propagation
- Materialization coordination
- Transaction handling

Runtime tests should replace external dependencies with test doubles where practical.

---

## Native AOT Tests

Because Native AOT is a core design goal, compatibility should be validated regularly.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Generated materializers
- Metadata provider
- Planner registry

No runtime reflection should be introduced.

---

## Continuous Integration

Every pull request should execute:

- Unit tests
- Generator tests
- Snapshot tests
- Integration tests
- Native AOT validation (where supported)

Builds should fail if generated snapshots change unexpectedly.

---

---

## Related Documentation

- [Code Style](Code-Style.md)
- [Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [Code Style](Code-Style.md)  |  Next: [ADR Process](ADR-Process.md) →
