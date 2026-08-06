[Home](../../README.md) → [Documentation](../README.md) → **Source Generators**

# Source Generators

The Roslyn incremental source generator is what makes Coffee Beanery's "compile-time first"
principle real rather than aspirational. It reads your EF Core mapping classes and emits the
runtime's execution plan — no reflection, no runtime model discovery.

---

## Contents

- [Mapping Generator](Mapping-Generator.md) — the concrete generator shipped today, and what it requires of your mapping code
- [Diagnostics](Diagnostics.md) — the CBMAP diagnostic codes and known risk areas
- [Pipeline Stages](Pipeline-Stages.md) — the 12-stage compilation pipeline

---

## Philosophy

## Philosophy

The Generator has one responsibility:

> Analyze once during compilation so Runtime never has to analyze again.

Everything expensive should happen here.

Runtime should consume generated artifacts rather than discover application structure dynamically.

---

---

## Related Documentation

- [Foundation → Metadata](../03-Foundation/Metadata.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [GraphQL](../05-GraphQL/README.md)  |  Next: [Dependency Injection](../07-Dependency-Injection/README.md) →
