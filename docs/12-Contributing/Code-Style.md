# Code Style

## Architecture first

Readable code is not enough.

A change must preserve the dependency direction.

## Prefer

- small immutable records;
- explicit contracts;
- deterministic transformations;
- descriptive names;
- focused methods;
- tests for invariants.

## Avoid

- service locators;
- hidden global state;
- provider-specific logic in planners;
- reflection when metadata can be explicit;
- speculative abstractions;
- domain-specific `if` statements inside generic planners.

## Comments

Comments should explain architectural intent or non-obvious invariants.

Do not restate obvious code.

## Naming

Use domain-neutral names in reusable projects.

Do not name core abstractions after:

- GraphQL;
- SQLite;
- PostgreSQL;
- a specific AI provider.

unless the project itself is that adapter.
