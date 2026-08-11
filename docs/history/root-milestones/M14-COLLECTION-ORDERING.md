# M14 — Collection Ordering

M14 adds explicit aggregate semantics for ordering a parent entity through a collection relationship.

Supported aggregates:

- `COUNT` — number of related rows
- `MIN` — minimum value of a related scalar field
- `MAX` — maximum value of a related scalar field

The semantic contract represents the aggregate; SQL remains a provider concern.

Example semantic order:

```text
Customer
  Accounts[]
    COUNT DESC
```

GraphQL adapter syntax:

```graphql
customer(order: {
  accounts: {
    _count: DESC
    balance: { max: ASC }
  }
})
```

The SQL provider translates a collection aggregate into a correlated scalar subquery. For example:

```sql
ORDER BY (
  SELECT COUNT(*)
  FROM "Account" "a0_agg"
  WHERE "a0_agg"."CustomerId" = "t0"."Id"
) DESC,
"t0"."Id" ASC
```

The aggregate is also supported as a component of the M12 compound cursor. Cursor values are therefore the aggregate result plus the deterministic root primary-key tie breaker.

M14 intentionally does not support aggregate traversal through multiple collection hops or implicit aggregate definitions. Those require additional semantic decisions and are deferred rather than inferred from SQL behavior.
