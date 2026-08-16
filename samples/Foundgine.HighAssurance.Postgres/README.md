# Foundgine.HighAssurance.Postgres

Real PostgreSQL execution for the M16 `TransferFunds` capability.

This project intentionally keeps the business semantic contract in `Foundgine.HighAssurance.Banking` and adds only the provider boundary: transaction, row locking, idempotency serialization, database mutation, audit persistence, and execution evidence.

Set `FOUNDGINE_POSTGRES_CONNECTION` when running the integration tests.
