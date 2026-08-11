# M10 — Relationship Filters

M10 ports the proven relationship-predicate capability without importing the old planner/filter architecture.

## Supported semantic predicates

- `some`
- `none`
- `all`
- nested field predicates
- nested `and` / `or`

## Provider boundary

The semantic layer contains only `SemanticRelationshipFilter` and its quantifier. The SQL provider translates these predicates into correlated `EXISTS` / `NOT EXISTS` expressions.

## Example

`Customer` where some `Accounts` has `Balance = 100.50` becomes conceptually:

```text
Customer
  WHERE EXISTS (
      Account
      WHERE Account.CustomerId = Customer.Id
        AND Account.Balance = @p0
  )
```

No SQL concepts are introduced into `Foundgine.Semantics`.
