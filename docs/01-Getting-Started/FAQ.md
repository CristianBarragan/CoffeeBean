[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **FAQ**

# Getting Started FAQ

## Contents

- [Do I need to learn a new modeling API?](#do-i-need-to-learn-a-new-modeling-api)
- [Does this replace Hot Chocolate or Dapper?](#does-this-replace-hot-chocolate-or-dapper)
- [Is this production-ready?](#is-this-production-ready)
- [Where's the full FAQ?](#wheres-the-full-faq)

---

## Do I need to learn a new modeling API?

No. Phase 1 uses **EF Core mapping classes** as the metadata source — see
[First Service → Write a mapping class](First-Service.md#write-a-mapping-class). Coffee
Beanery reads that mapping at compile time; it doesn't ask you to learn a parallel schema
language.

## Does this replace Hot Chocolate or Dapper?

No — it deliberately doesn't. Hot Chocolate remains the GraphQL framework, Dapper remains
the SQL executor. Coffee Beanery sits between your domain model and those tools, generating
the execution plan that connects them. See
[Architecture → Vision](../02-Architecture/Vision.md#what-coffee-beanery-is) for the full
positioning.

## Is this production-ready?

Treat it as early-stage. The mapping source generator is explicitly marked
**"not yet build-verified"** against arbitrary mapping shapes beyond the sample — see
[Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md#known-risk-areas).
Review the [Roadmap](../13-Reference/Roadmap.md) and [ADRs](../13-Reference/ADRs.md) before
depending on it for anything load-bearing.

## Where's the full FAQ?

The extended architecture FAQ — covering source generators vs. reflection, transport and
provider independence, and Native AOT — lives in
**[Reference → FAQ](../13-Reference/FAQ.md)**.

---

## Related Documentation

- [Reference → FAQ](../13-Reference/FAQ.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)
- [Reference → ADRs](../13-Reference/ADRs.md)

---

← Previous: [Configuration](Configuration.md)  |  Next: [Architecture](../02-Architecture/README.md) →
