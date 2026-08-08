[Home](../../README.md) → [Documentation](../README.md) → **Foundation**

# Foundation

Foundation is the dependency-free contract layer everything else in Foundgine builds
on. It defines *what* the system talks about — metadata shapes, planning primitives,
interfaces, identifiers — without knowing *how* any of it gets executed.

---

## Contents

- [Metadata](Metadata.md) — the compile-time knowledge Foundation defines
- [Contracts](Contracts.md) — interfaces, planning primitives, identifiers
- [Components](Components.md) — project structure and responsibilities
- [Extensibility](Extensibility.md) — the extension points Foundation exposes to providers and transports

---


## Philosophy

## Philosophy

Foundation answers one question:

> **What exists?**

It deliberately does **not** answer:

- How queries execute
- How SQL is generated
- How GraphQL works
- How metadata is discovered

Those responsibilities belong to higher layers.

---

---

## Related Documentation

- [Architecture → Layers](../02-Architecture/Layers.md)
- [Runtime](../04-Runtime/README.md)
- [Dependency Injection](../07-Dependency-Injection/README.md)

---

← Previous: [Architecture](../02-Architecture/README.md)  |  Next: [Runtime](../04-Runtime/README.md) →
