> Source content for [`index.html`](index.html), the page actually served on the site. Edit this file, then regenerate the HTML page and `llms-full.md`.

# Foundgine Performance

## CoffeeBeanery PostgreSQL graph benchmark — 12 August 2026

Three independent successful runs were performed against a deterministic PostgreSQL graph workload.

### Workload

```text
Customer
  → CustomerBankingRelationship
      → Contract
          → Transaction
```

Fixture:

- 1,000 customers
- 4,000 relationships
- 12,000 contracts
- 48,000 transactions
- concurrency 1, 8, 32
- 10-second measurement per case
- 3-second warm-up
- 5-second request timeout

## Query result

At concurrency 32:

| Implementation | Average RPS | Average p95 |
|---|---:|---:|
| Hot Chocolate + EF Core | 139.4 | 338.4 ms |
| Foundgine — no cache | 2,781.0 | 20.3 ms |
| Foundgine — provider-plan cache | 2,838.9 | 19.9 ms |

That is approximately:

- 20.0× the throughput without the cache
- 20.4× with the cache
- 16.7× lower p95 latency without the cache
- 17.0× lower p95 latency with the cache

The large query advantage is therefore not dependent on provider-plan caching.

## Reliability

The three successful runs reported:

- 0 application errors
- 0 request timeouts
- 0 cancelled requests

## Mutation results

Mutation performance is more variable.

The benchmark supports the conclusion that Foundgine can perform well at higher concurrency, but mutation performance should not currently be presented as the primary performance claim.

## What this proves

The strongest evidence is for:

> **read/query execution over a relationship-heavy PostgreSQL graph workload.**

The results consistently show substantially higher query throughput and lower p95 latency in this controlled workload.

## What this does not prove

This is not a universal benchmark of every:

- EF Core workload
- Hot Chocolate workload
- PostgreSQL schema
- query shape
- mutation workload
- hardware configuration

Results depend on the workload, schema, provider versions, host, fixture, and implementation versions.

The appropriate claim is:

> **Foundgine demonstrates a substantial performance advantage for this relationship-heavy graph query workload.**
