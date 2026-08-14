# PostgreSQL E2E — Measurement Gate Recommendation

PostgreSQL E2E is now treated as the flagship integration proof, not as a compiler optimization experiment.

## Proof sequence

```text
Semantic mutation
  -> execution IR
  -> PostgreSQL compiler
  -> PostgreSQL 17
  -> query observes state
  -> semantic mutation changes state
  -> query observes changed state
  -> rollback
```

## Isolation

PostgreSQL E2E tests create a unique schema per test connection and set `search_path` to that schema. This avoids destructive interaction with `public` and makes repeated/parallel runs safer. The test database itself must still be dedicated to Foundgine integration tests.

## Measurement matrix

### Mutation

| Batch | Depth |
|---:|---:|
| 1 | 1 |
| 10 | 1 |
| 50 | 2 |
| 500 | 3 |

### Query

The query dimension is called `dataset`, not `batch`: it scales the number of seeded rows rather than batching requests.

| Dataset | Depth |
|---:|---:|
| 1 | 1 |
| 10 | 1 |
| 50 | 2 |
| 500 | 3 |

## PostgreSQL evidence

Each `EXPLAIN (ANALYZE, BUFFERS, WAL, FORMAT JSON)` run records:

- planning time
- execution time
- shared hit/read/written blocks
- temp read/write blocks
- WAL bytes/records/FPI where available
- join strategies
- sort count and sort method/space
- materialization count
- per-node estimated rows
- per-node actual rows
- actual loops
- actual total time

The five largest estimated/actual row-ratio nodes are printed so planner misestimates are visible instead of being hidden by root-node totals.

## Interpretation gate

Do not modify `PostgresBatchedMutationCompiler` or the semantic/execution architecture until the matrix has been collected from PostgreSQL 17.

The first optimization decision should answer which layer is responsible:

1. semantic/planning overhead before SQL,
2. SQL shape and generated correlation strategy,
3. PostgreSQL planner choice,
4. row-estimate error,
5. unnecessary sort/materialization,
6. physical I/O/WAL pressure.

Only evidence from the matrix should determine the next compiler change.
