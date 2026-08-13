# SQL mutations

Compiles provider-neutral mutation plans into parameterized SQL.

The provider owns SQL details such as `INSERT`, `UPDATE`, `DELETE`, conflict handling, and generated identity retrieval.


## PostgreSQL batched mutations

`PostgresBatchedMutationCompiler` and `PostgresBatchedMutationExecutionProvider` are the
PostgreSQL-specific fast path for mutation batches.

- Create/Upsert operations at the same dependency level are grouped and sent as one statement.
- Literal values are bound as PostgreSQL arrays and expanded with `unnest(... WITH ORDINALITY)`.
- Generated/reference values flow through ordinal maps instead of client round trips.
- Update/Delete operations remain individual CTEs but share the same physical statement.
- If a batch is not safely batchable, the provider falls back to `SqlMutationCompiler` /
  `SqlMutationExecutionProvider`.
- The PostgreSQL provider is selected explicitly by the caller; it does not inspect connection
  runtime types.

The physical result contract is `__grp` + `__ord`, which lets the executor reconstruct the
original `MutationResult` list without comparing JSON/business-key values.
