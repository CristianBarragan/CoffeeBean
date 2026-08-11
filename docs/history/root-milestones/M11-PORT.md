# M11 Port — Cursor Pagination

Ported from the archive: the stable primary-key keyset technique.

New Foundgine contracts:

```text
SemanticQueryOptions.After
EntityMetadata.PrimaryKey
ExecutionPageInfo
SqlPaginationPlan
```

The semantic layer never sees SQL `>` predicates or column names.
