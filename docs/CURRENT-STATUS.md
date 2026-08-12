# Current status

Foundgine has completed its ground-up foundation and has a working end-to-end proof.

## Proven

- semantic entities, fields, relationships, and identities;
- request resolution and granular semantic authorization;
- entity/field/relationship read/write capability discovery;
- conditional authorization predicates preserved into execution plans;
- mutation write authorization;
- provider-independent query planning;
- CRUD/upsert mutation planning;
- nested/dependency-aware mutation planning;
- SQL compilation and SQLite execution;
- AOT metadata generation;
- JSON structured intent;
- GraphQL queries and mutations through Hot Chocolate;
- GraphQL variables, fragments, aliases, directives, operation selection, input coercion, schema generation, and structured adapter errors;
- relationship filters/order, aggregate filters/order, and cursor pagination covered by tests.

## M39 architectural rule

Authorization is semantic execution state. Capability discovery is advisory
context for callers such as AI agents; it never replaces authorization at
execution time. Conditional predicates remain in the provider-independent plan
so future plan caching cannot accidentally cache away tenant or user-specific
constraints.

## Main architectural rule

Adapters produce semantic contracts. Providers consume plans. GraphQL and SQL do not enter the semantic core.

## Not claimed

Foundgine is not yet presented as a production-ready autonomous-agent platform, universal database provider, ORM replacement, or benchmark winner.

Those claims require additional implementation and evidence.

## Source of truth

The current source and passing tests define reality. Historical milestone documents under `docs/history` explain how the repository got here.

## M40 architectural rule

Compiled provider plans may be cached only **after** semantic authorization has
completed. The cache key represents the complete authorized execution plan,
including authorization predicates and exact request values. Runtime execution
context is deliberately not cached; providers resolve context-bound authorization
parameters at execution time.
