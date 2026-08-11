# M13 — Relationship-Aware Ordering

M13 ports the proven navigation-ordering technique from the archived Graphgine implementation into the new Foundgine contracts.

## Supported

- Ordering by a field on a selected `One` relationship.
- Nested semantic order paths represented by `RelationshipId` values.
- Compound keyset cursors containing the nested sort value plus the root primary-key tie breaker.
- SQL `ORDER BY` and lexicographic seek predicates resolved against the execution-plan node aliases.

Example:

```graphql
query {
  customer(order: { profile: { displayName: DESC } }) {
    id
    profile { displayName }
  }
}
```

becomes a semantic order term equivalent to:

```text
Path: Customer -> Profile
Field: Profile.DisplayName
Direction: DESC
```

## Deliberately deferred

Ordering through `Many` relationships is rejected. A collection does not have one scalar value with which to order the parent row; it requires explicit aggregate semantics such as `MIN`, `MAX`, or a defined first/last rule. Foundgine will not silently invent that meaning.

Ordering by an unselected relationship is also rejected. The provider does not introduce an implicit join solely for ordering. This preserves the same boundary used by the archived implementation while keeping the new semantic model explicit.

## Provider boundary

The semantic layer contains only relationship identities, field identity, and sort direction. SQL aliases, storage columns, joins, and seek predicates are resolved inside `Foundgine.Sql`.
