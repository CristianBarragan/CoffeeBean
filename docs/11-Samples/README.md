[Home](../../README.md) → [Documentation](../README.md) → **Samples**

# Samples

## Contents

- [The Banking sample](#the-banking-sample)
- [Solution layout](#solution-layout)
- [Running it](#running-it)

---

## The Banking sample

`example/HotChocolateCoffeeBeanery` is the one sample in the repository today, and it's the
canonical reference for every layer of Coffee Beanery working together: EF Core mapping
classes, the [mapping generator](../06-Source-Generators/Mapping-Generator.md), the
[runtime](../04-Runtime/README.md), [Hot Chocolate](../05-GraphQL/README.md), and
[PostgreSQL + Apache AGE](../08-Persistence/PostgreSQL-AGE.md), modeling a small Banking
domain (customers, accounts, contracts, transactions).

It uses:

- Dapper
- Hot Chocolate
- Entity Framework (as the mapping source, not the execution engine — see
  [Persistence → Dapper & EF Core](../08-Persistence/Dapper-EFCore.md))
- PostgreSQL
- FasterKV (in-process cache)

## Solution layout

| Project | Role |
|---|---|
| `Api/Api.Banking` | ASP.NET Core host, GraphQL endpoint, query/mutation resolvers |
| `Domain/CoffeeBeanery` | The framework runtime |
| `Domain/CoffeeBeanery.GraphQL.Core.Foundation` | Foundation contracts |
| `Domain/CoffeeBeanery.GraphQL.Core.Mapping.Generators` | The Roslyn mapping generator |
| `Domain/Domain.Model`, `Domain/Domain.Shared` | Business/domain model and mapping DSL |
| `Infrastructure/Command` | Command-side infrastructure |
| `Infrastructure/Database/Database.Entity*` | EF Core entity models + migrations (relational) |
| `Infrastructure/Database/Database.Graph*` | Apache AGE graph models + migrations |
| `Test` | Test project |

## Running it

See [Getting Started → Installation](../01-Getting-Started/Installation.md) and
[Getting Started → First Service](../01-Getting-Started/First-Service.md) for the full
walkthrough, including PostgreSQL/AGE setup.

---

## Related Documentation

- [Getting Started](../01-Getting-Started/README.md)
- [GraphQL](../05-GraphQL/README.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Performance](../10-Performance/README.md)  |  Next: [Contributing](../12-Contributing/README.md) →
