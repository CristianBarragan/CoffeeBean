# CoffeeBeanery Performance — Sequential Query Benchmark

The query benchmark intentionally runs **one workload target at a time**.

1. PostgreSQL starts and becomes healthy.
2. The database initializer runs once, applies EF migrations, validates/seeds the deterministic fixture, and exits.
3. Hot Chocolate starts, is benchmarked, and is stopped.
4. Foundgine cold starts, is benchmarked, and is stopped.
5. Foundgine warm starts, is benchmarked, and is stopped.
6. PostgreSQL is stopped.

The benchmark driver (`CoffeeBeanery.LoadTest`) runs on the host. It is not a Docker Compose service.

## Run

From `C:\Foundgine\benchmarks\CoffeeBeanery.Performance`:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\run-query.ps1
```

Or:

```powershell
.\pipelines\query.ps1
```

The pipeline expects the repository layout:

```text
C:\Foundgine
  src\...
  benchmarks\CoffeeBeanery.Performance\...
```

Reports are written to:

```text
reports\query\hotchocolate
reports\query\foundgine-cold
reports\query\foundgine-warm
```

## Important

Only PostgreSQL is defined in `compose/postgres.yml`. API containers are deliberately started and removed by `pipelines/query.ps1`, so a failure in one API cannot cause Compose to start or stop the other APIs.
