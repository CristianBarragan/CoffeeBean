# Foundgine.Sql

The current SQL provider.

It translates Foundgine plans into parameterized SQL and executes them through ADO.NET.

SQL-specific concepts start here; they do not leak into the semantic or planning layers.

## Semantic retrieval and grounding

`Foundgine.Sql.Retrieval.PostgresRetrievalCandidateSource` is the PostgreSQL implementation of the semantic candidate boundary. It produces ranked candidates and provenance evidence rather than bypassing the semantic planner.

Supported strategies:

- `Fuzzy` -> `pg_trgm`
- `FullText` -> PostgreSQL `tsvector` / `websearch_to_tsquery`
- `Search` -> optional `pg_search` / BM25
- `GraphSimilarity` -> optional Apache AGE
- `Vector` -> intentionally reserved for a future `pgvector` provider

The semantic layer remains provider-neutral. Retrieval grounds ambiguous references; authorization and final relational execution remain deterministic Foundgine operations.
