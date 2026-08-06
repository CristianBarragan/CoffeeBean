[Home](../../README.md) → [Documentation](../README.md) → **Dependency Injection**

# Dependency Injection

## Contents

- [Registration](Registration.md) — the composition root and per-layer registration
- [Lifetimes](Lifetimes.md) — lifetime guidelines and testing

---

## Philosophy

## Philosophy

Dependency Injection answers one question:

> **How are framework components composed?**

It should never determine:

- Query behavior
- SQL generation
- Planning logic
- Metadata construction

Those responsibilities belong elsewhere.

---

## Architectural Role

## Architectural Role

Dependency Injection sits at the composition root.

```
Application

↓

Dependency Injection

↓

Runtime

↓

Foundation Contracts

↓

Generated Implementations
```

Runtime depends only upon abstractions.

Applications decide which implementations to register.

---

---

## Related Documentation

- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)
- [Getting Started → Configuration](../01-Getting-Started/Configuration.md)

---

← Previous: [Source Generators](../06-Source-Generators/README.md)  |  Next: [Persistence](../08-Persistence/README.md) →
