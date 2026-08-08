# Samples

## Banking sample

The current sample is:

`samples/Graphgine.Samples.Banking`

It demonstrates the intended combination of:

- domain models
- Entity Framework Core mapping
- Graphgine source generation
- Hot Chocolate
- PostgreSQL / Npgsql
- Apache AGE-oriented graph support

The domain includes customers, contact points, contracts, accounts, transactions, products and
customer-to-customer relationships.

## Important status note

The Banking sample still contains historical wiring from the Coffee Beanery implementation and
must be repaired and validated before it is advertised as a guaranteed clone-and-run example.

In particular, sample references to old service-registration and processing abstractions should be
treated as migration work rather than current platform API guarantees.

## Source of truth

Use the current project files under `samples/Graphgine.Samples.Banking` and the `src/` projects
rather than historical paths in `legacy/`.

## Related

- [Getting Started](../01-Getting-Started/README.md)
- [GraphQL](../05-GraphQL/README.md)
- [Persistence](../08-Persistence/README.md)
- [Source Generators](../06-Source-Generators/README.md)
