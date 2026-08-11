# M15 — Aggregate Filters

M15 adds semantic filtering over collection relationship aggregates.

## Supported semantics

- `COUNT(collection)`
- `MIN(collection.field)`
- `MAX(collection.field)`
- `eq`, `neq`, `gt`, `gte`, `lt`, `lte`

Example semantic filter:

```text
Customer
  Accounts COUNT >= 2
```

or:

```text
Customer
  Accounts.Balance MAX > 100
```

## GraphQL translation

```graphql
customer(
  where: {
    accounts: {
      count: { gte: 2 }
    }
  }
)
```

and:

```graphql
customer(
  where: {
    accounts: {
      balance: {
        max: { gt: 100 }
      }
    }
  }
)
```

The GraphQL adapter converts these into `SemanticAggregateFilter` instances. No GraphQL AST concepts cross the adapter boundary.

## SQL boundary

The SQL provider renders aggregates as correlated scalar subqueries. For example:

```sql
WHERE (
  SELECT COUNT(*)
  FROM "Account" "a0"
  WHERE "a0"."CustomerId" = "t0"."Id"
) >= @p0
```

For `MAX`:

```sql
WHERE (
  SELECT MAX("a0"."Balance")
  FROM "Account" "a0"
  WHERE "a0"."CustomerId" = "t0"."Id"
) > @p0
```

The semantic model does not contain SQL operators, aliases, tables, or columns.

## Deliberate limits

M15 does not add `SUM`, `AVG`, nested collection aggregates, aggregate expressions, or aggregate-filtered aggregates. Those should only be introduced when a concrete semantic requirement justifies them.
