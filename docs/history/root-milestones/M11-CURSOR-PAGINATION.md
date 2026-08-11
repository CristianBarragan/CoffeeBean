# M11 — Forward Keyset Pagination

M11 adds provider-neutral forward cursor pagination. The semantic request carries an opaque `After` cursor; the SQL provider interprets it using root primary-key metadata.

## Contract

- `SemanticQueryOptions.After` is an opaque cursor string.
- `Limit`/GraphQL `first` is the page size.
- A root `EntityMetadata.PrimaryKey` is required.
- When keyset pagination is active, ordering defaults to the root primary key ascending.
- The SQL provider fetches one extra row to calculate `HasNextPage`.
- The first/last primary-key values become opaque cursors.

## Intentionally deferred

- `before` / `last` backward pagination
- compound cursors for arbitrary user ordering
- mixed-direction keyset predicates
- navigation-field ordering combined with cursors

These were not copied from the archive because they require additional semantic and metadata contracts. The archived implementation was used as evidence for the stable-primary-key seek technique only.
