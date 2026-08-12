# Foundgine ON CONFLICT merge optimization

## Mutation SQL

Foundgine upserts now compile to the PostgreSQL-style merge shape:

```sql
INSERT INTO "products" ("sku", "name", "price")
VALUES (@p0, @p1, @p2)
ON CONFLICT ("sku")
DO UPDATE SET
    "name" = @p1,
    "price" = @p2
WHERE
    "products"."name" IS DISTINCT FROM EXCLUDED."name"
    OR "products"."price" IS DISTINCT FROM EXCLUDED."price"
RETURNING ...;
```

`IS DISTINCT FROM` is null-safe and prevents a physical update when all incoming values already match the stored row.

### RETURNING preservation

A PostgreSQL `ON CONFLICT DO UPDATE ... WHERE` that evaluates to false does not emit a `RETURNING` row. Foundgine needs that row for mutation result materialization and dependent child mutations.

Therefore the compiled `SqlMutationPlan` carries a fallback `SELECT` for the no-change case. The execution provider runs it only when the upsert returned no row. This preserves mutation dependencies while avoiding the unnecessary UPDATE.

## Conflict indexes

The CoffeeBeanery benchmark schema already has unique indexes suitable for its business-key conflict identities:

| Entity | Conflict identity | EF configuration |
|---|---|---|
| Customer | `CustomerKey` | `HasIndex(x => x.CustomerKey).IsUnique()` |
| CustomerBankingRelationship | `CustomerBankingRelationshipKey` | `HasIndex(x => x.CustomerBankingRelationshipKey).IsUnique()` |
| Contract | `ContractKey` | `HasIndex(x => x.ContractKey).IsUnique()` |
| Transaction | `TransactionKey` | `HasIndex(x => x.TransactionKey).IsUnique()` |
| Account | `AccountKey` | `HasIndex(x => x.AccountKey).IsUnique()` |
| ContactPoint | `ContactPointKey` | `HasIndex(x => x.ContactPointKey).IsUnique()` |
| CustomerCustomerRelationship | `CustomerCustomerRelationshipKey` | unique |
| CustomerCustomerRelationship | `(OuterCustomerId, InnerCustomerId)` | unique composite |

PostgreSQL can use these unique indexes as `ON CONFLICT` arbiters.

## Graph traversal indexes

The benchmark also adds non-unique traversal indexes:

- `CustomerBankingRelationship(CustomerId, Id)`
- `Contract(CustomerBankingRelationshipId, Id)`
- `Transaction(ContractId, Id)`

These are separate from conflict indexes. They optimize the read graph traversal and should not be marked unique.
