# M19 — Nested / Relationship Mutations

M19 adds a provider-neutral mutation tree that is flattened into the existing M18 dependency-aware mutation batch.

A nested mutation such as Customer -> Account -> Transaction resolves relationship metadata, injects parent primary-key values into child foreign-key columns, and produces the same `MutationBatchPlan` used by M18.

The SQL provider remains unchanged: it executes the flattened dependency batch atomically.

M19 deliberately does not introduce GraphQL mutation syntax, collection mutation semantics, delete cascades, or relationship-specific SQL.
