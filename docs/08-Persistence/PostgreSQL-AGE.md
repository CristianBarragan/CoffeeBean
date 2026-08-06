[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **PostgreSQL & AGE**

# PostgreSQL & AGE

## Contents

- [Why PostgreSQL is Phase 1](#why-postgresql-is-phase-1)
- [Apache AGE and the graph read path](#apache-age-and-the-graph-read-path)
- [Provider Architecture](#provider-architecture)
- [SQL Writers, Readers & Dialects](#sql-writers-readers--dialects)

---

## Why PostgreSQL is Phase 1

PostgreSQL is Coffee Beanery's first execution provider, wired through Npgsql
(`Npgsql.EntityFrameworkCore.PostgreSQL`). Nothing in the [runtime](../04-Runtime/README.md)
or [Foundation contracts](../03-Foundation/Contracts.md) assumes PostgreSQL specifically —
see [Provider Architecture](#provider-architecture) below — but it's the only provider that's
actually implemented and tested today.

## Apache AGE and the graph read path

The sample's `Database.Graph.Banking` project layers [Apache AGE](https://age.apache.org/)
(a graph extension for PostgreSQL) on top of the relational schema, exposed through
`AgeConnectionFactory`, `GraphMap`, `Edge`, and `LinkKey` in `GraphQL/Core/Sql`. This is what
lets the GraphQL schema's node/edge shape (see [GraphQL → Schema](../05-GraphQL/Schema.md))
map naturally onto graph traversal for relationship-heavy queries, without hand-written
recursive joins.

## Provider Architecture

> Providers are the abstraction layer between the CoffeeBeanery Runtime and a specific persistence technology. They translate execution plans into provider-specific operations while preserving the semantics established during planning. Providers understand databases, transports, and protocols—but they never understand the application's domain model.

Providers encapsulate infrastructure.

They do not own business logic.

---

## Philosophy

Providers follow one rule:

> **The Runtime understands execution. Providers understand infrastructure.**

Responsibilities should never overlap.

---

## Why Providers?

Without providers:

```
Runtime

↓

PostgreSQL

↓

Execution
```

Supporting another database requires modifying Runtime.

With providers:

```
Runtime

↓

IProvider

↓

PostgreSQL

SQL Server

SQLite

MySQL
```

Runtime never changes.

---

## High-Level Architecture

```
Execution Plan

↓

Runtime

↓

Provider

↓

Infrastructure

↓

Results
```

Providers isolate infrastructure concerns.

---

## Provider Responsibilities

Providers are responsible for:

- SQL serialization
- Connection management
- Command execution
- Parameter binding
- Transaction integration
- Result stre

## SQL Writers, Readers & Dialects

## SQL Writers

SQL writers serialize execution plans.

Typical responsibilities:

- SELECT
- INSERT
- UPDATE
- DELETE
- UPSERT
- RETURNING
- Common Table Expressions (CTEs)

Writers should remain declarative.

---

## SQL Readers

Readers convert raw database results into structures suitable for materialization.

Responsibilities include:

- DbDataReader helpers
- Typed value access
- Database-specific conversions

Readers should not construct application models.

---

## SQL Dialects

Database-specific syntax belongs to dialect implementations.

Example interface:

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteOffset(...);

    void WriteConflict(...);

    void WriteReturning(...);
}
```

Supported dialects may include:

- PostgreSQL
- SQL Server
- SQLite
- MySQL
- Oracle

---

---

## Related Documentation

- [Dapper & EF Core](Dapper-EFCore.md)
- [GraphQL → Schema](../05-GraphQL/Schema.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Persistence](README.md)  |  Next: [Dapper & EF Core](Dapper-EFCore.md) →
