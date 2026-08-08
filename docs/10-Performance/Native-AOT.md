# Native AOT

Native AOT is an architectural consideration, not a currently verified product claim.

## Why it matters

A compile-time-oriented execution model can reduce runtime discovery and make Native AOT easier to
support. It can also improve startup characteristics when dynamic infrastructure is avoided.

## Current design direction

The project prefers:

- generated metadata
- generated registries
- explicit provider plans
- deterministic SQL generation
- source-generated serialization where appropriate
- minimal runtime assembly scanning

## Do not make this claim yet

The repository should **not** currently be marketed as:

- fully Native AOT compatible
- zero-reflection everywhere
- zero-runtime-code-generation everywhere

Those statements require repeatable AOT builds and integration tests.

## Validation required

A future AOT CI job should verify:

1. compilation
2. application startup
3. metadata resolution
4. query execution
5. mutation execution
6. materialization
7. GraphQL request execution

See [Source Generators](../06-Source-Generators/README.md) and
[Performance](README.md).
