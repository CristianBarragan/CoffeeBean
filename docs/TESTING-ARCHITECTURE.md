# Foundgine testing architecture

Foundgine treats PostgreSQL/EF Core as a **canonical integration-test oracle** for
its relational execution boundary. EF is not part of the runtime architecture;
it is the independently expressed reference implementation used to detect semantic
and relational regressions.

## Test layers

| Layer | Purpose | Database |
|---|---|---|
| Unit | Pure semantics/planning/security invariants | No |
| Component | Provider/compiler/adapters | Usually no |
| EF/POSTGRES | Real relational execution + EF differential oracle | PostgreSQL 17 + pgvector |
| E2E | Full application/provider flows | Optional PostgreSQL |
| Security | Authorization and adversarial invariants | Mostly no; selected suites use PostgreSQL |
| AOT | Generator/runtime parity | No |
| GraphQL | Transport adapter contract | No |
| MCP | Agent/tool boundary contract | No |
| PENTEST | Authorized live deployment checks | External deployment |

## Canonical PostgreSQL fixture

`tests/Foundgine.Testing/PostgresFixture.cs` owns the database lifecycle used by
new integration tests. The repository Docker compose file owns schema creation;
tests never create or drop the shared schema.

Each test explicitly calls `ResetCanonicalQueryDataAsync`, which truncates the
canonical workload and inserts deterministic rows. This makes tests repeatable and
keeps test data independent from benchmark data.

## Differential oracle

`FoundginePostgresHarness` executes the semantic request through:

```text
Semantic model
  -> authorization
  -> plan
  -> SQL compiler
  -> PostgreSQL
```

The same scenario is expressed independently through EF Core LINQ. The test compares
observable results rather than SQL text. This is deliberate: SQL shape is an
implementation detail; semantic equivalence is the contract.

## One-command gate

From the repository root:

```powershell
.\test-all.ps1
```

The gate automatically uses Docker/PostgreSQL when available. Tests that require
PostgreSQL are clearly separated from database-free tests. Set
`FOUNDGINE_TARGET_HOST` and `FOUNDGINE_TARGET_URL` to include the live authorized
penetration gate.


## P2 — PostgreSQL security authority

The security suite treats the live PostgreSQL transaction boundary as authoritative for mutation state.
The P2 conformance coverage explicitly verifies tenant isolation, ownership authorization, mutation invariants,
idempotency/replay, concurrent same-key linearization, and frozen-resource rejection. Rejected operations must
leave balances, idempotency records, and audit records unchanged.

These tests intentionally do not replace the semantic security/unit tests: they validate the final relational
execution boundary against the actual database.
