# Coffee Beanery — Benchmark Results

> **Conditions:** No application-level caching. PostgreSQL built-in query cache only.
> **Tool:** Apidog
> **Date:** June 2026

---

## Overview

Each test executes a full round-trip against a live PostgreSQL instance:

1. **Mutation (upsert)** — inserts or updates all entities across the full relationship graph using `INSERT ... ON CONFLICT DO UPDATE`
2. **Filtered query** — a single batched `SELECT` with nested `LEFT JOIN` / `JOIN` chains that resolves the entire object graph in one database call
3. **Entity-to-model mapping** — the raw Dapper rows are mapped back to domain models using pre-compiled expression delegates, with zero reflection cost at request time

The `Product` model in these tests spans **4 physical tables** (`Banking.CustomerBankingRelationship`, `Lending.Contract`, `Account.Account`, `Lending.Transaction`). A single GraphQL query touching one customer with one product generates:

- **10 upsert statements** (writes across all 4 tables + relationship resolution steps)
- **1 SELECT** joining 5 tables across 3 schemas with 4 levels of nesting — resolved in a single database round trip
- **0 reflection overhead** at mapping time — all property access goes through pre-compiled lambda delegates

---

## Why Response Times Are This Low

### Startup Warmup Pipeline

Before the first request is served, `GraphWarmup.Init` executes a full warmup pipeline:

1. **Mapping set discovery** — scans the assembly for all `IMappingSet` implementations and registers them against both enum axes (model type + entity type)
2. **Property cache population** — `MappingWarmup.WarmupMap` walks every `FieldMap` and stores the resolved `PropertyInfo` objects in `NodeMap.ModelProperties` and `NodeMap.EntityProperties`, eliminating per-request `Type.GetProperty` calls
3. **Delegate compilation** — `BulkMapper.Compile` builds `Expression`-based getter and setter delegates compiled to IL via `Expression.Lambda.Compile()`, stored in `ConcurrentDictionary` keyed by `TypeFullName.PropertyName`
4. **NodeTree generation** — `NodeTreeIterator.GenerateTree` pre-builds the full traversal tree for every root mapping, so query planning at request time walks a pre-computed structure rather than reflecting over types

By the time the first GraphQL request arrives, the mapping layer has no reflection work left to do. Every property read and write goes through a cached compiled delegate.

### Request-Time Execution

At request time the three phases are:

| Phase | Mechanism | Reflection cost |
|---|---|---|
| SQL generation | Pre-built NodeTree traversal + string assembly | None |
| PostgreSQL execution | Single batched statement via Dapper | None |
| Entity-to-model mapping | `Mapper.MapByAlias` → compiled getter/setter delegates | None |

The `Mapper` uses three `ConcurrentDictionary` caches — `_propCache`, `_getterCache`, `_setterCache` — populated during warmup. `MapByAlias` resolves the `NodeMap` by alias, iterates `FieldMaps`, and copies values using the pre-compiled delegates with no runtime reflection.

---

## Test 1 — Single Customer (`eq` filter)

**Scenario:** One customer with one product per dataset. Fully random data per iteration.

| Metric              | Value  |
|---------------------|--------|
| Datasets            | 5      |
| Iterations Executed | 5      |
| Iterations Failed   | 0      |
| Assertions Executed | 10     |
| Assertions Failed   | 0      |
| Pass Rate           | 100%   |
| Total Duration      | 239 ms |
| Max Response Time   | 67 ms  |
| Avg Response Time   | 13 ms  |

**Per-dataset response times:**

| Dataset   | Response Time |
|-----------|---------------|
| Dataset-1 | 15 ms         |
| Dataset-2 | 13 ms         |
| Dataset-3 | 13 ms         |
| Dataset-4 | 12 ms         |
| Dataset-5 | 14 ms         |

### GraphQL Request

```graphql
mutation a {
  wrapper(
    wrapper: {
      model: INNER_CUSTOMER
      customerCustomerEdge: [
        {
          innerCustomer: {
            customerKey: "{{CustomerKey1}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming1}}"
            fullNaming: "{{FullNaming1}}"
            lastNaming: "{{LastNaming1}}"
            product: [
              {
                accountKey: "{{AccountKey1}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey1}}"
                transactionKey: "{{TransactionKey1}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey1}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        }
      ]
      cacheKey: "2c0c7698-465f-4fbb-a8c1-9614f7ec6c05"
    }
    where: {
      customerCustomerEdge: {
        some: {
          innerCustomer: {
            customerKey: {
              eq: "{{CustomerKey1}}"
            }
          }
        }
      }
    }
  ) {
    edges {
      node {
        customerCustomerEdge {
          innerCustomer {
            customerKey
            customerType
            firstNaming
            fullNaming
            lastNaming
            product {
              contractKey
              accountName
              accountNumber
              amount
              balance
            }
          }
        }
      }
    }
  }
}
```

### Generated SQL

This single GraphQL mutation compiles into **10 upsert statements** followed by **1 SELECT**.

#### Phase 1 — Leaf entity upserts (no FK dependencies)

```sql
INSERT INTO "Lending"."Transaction" ("Amount", "Balance", "TransactionKey")
VALUES ('100', '1200', '9875df6c-42c3-4630-b944-c221de665a66')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "Amount" = EXCLUDED."Amount",
  "Balance" = EXCLUDED."Balance",
  "TransactionKey" = EXCLUDED."TransactionKey";

INSERT INTO "Account"."Account" ("AccountKey", "AccountName", "AccountNumber")
VALUES ('b14ed96f-466e-4176-a7d2-66f9088ac384', '123AN', '321AN')
ON CONFLICT ("AccountKey") DO UPDATE SET
  "AccountKey" = EXCLUDED."AccountKey",
  "AccountName" = EXCLUDED."AccountName",
  "AccountNumber" = EXCLUDED."AccountNumber";

INSERT INTO "Lending"."Contract" ("Amount", "ContractKey", "ContractType")
VALUES ('100', '76dea764-8c7f-474d-98bd-6833d0f92fb5', '0')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "Amount" = EXCLUDED."Amount",
  "ContractKey" = EXCLUDED."ContractKey",
  "ContractType" = EXCLUDED."ContractType";

INSERT INTO "Banking"."CustomerBankingRelationship" ("CustomerBankingRelationshipKey")
VALUES ('d94fbf63-fff5-4523-ba1c-9f12dae2c600')
ON CONFLICT ("CustomerBankingRelationshipKey") DO UPDATE SET
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey";

INSERT INTO "Banking"."Customer" ("CustomerKey", "CustomerType", "FirstName", "FullName", "LastName")
VALUES ('23e8761f-6373-434c-90fb-5359ffa93ff7', '0', 'Cristopher', 'Molly Greenholt', 'Hane')
ON CONFLICT ("CustomerKey") DO UPDATE SET
  "CustomerKey" = EXCLUDED."CustomerKey",
  "CustomerType" = EXCLUDED."CustomerType",
  "FirstName" = EXCLUDED."FirstName",
  "FullName" = EXCLUDED."FullName",
  "LastName" = EXCLUDED."LastName";
```

#### Phase 2 — Relationship resolution upserts (FK stitching via SELECT subqueries)

```sql
-- Resolve Customer → CustomerBankingRelationship
INSERT INTO "Banking"."CustomerBankingRelationship"
  ("CustomerId", "CustomerKey", "CustomerBankingRelationshipKey")
  (SELECT c."Id", c."CustomerKey", 'd94fbf63-fff5-4523-ba1c-9f12dae2c600'
   FROM "Banking"."Customer" c
   WHERE "CustomerKey" = '23e8761f-6373-434c-90fb-5359ffa93ff7')
ON CONFLICT ("CustomerBankingRelationshipKey") DO UPDATE SET
  "CustomerId" = EXCLUDED."CustomerId",
  "CustomerKey" = EXCLUDED."CustomerKey",
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey";

-- Resolve CustomerBankingRelationship → Contract
INSERT INTO "Lending"."Contract"
  ("CustomerBankingRelationshipId", "CustomerBankingRelationshipKey", "ContractKey")
  (SELECT cbr."Id", cbr."CustomerBankingRelationshipKey", '76dea764-8c7f-474d-98bd-6833d0f92fb5'
   FROM "Banking"."CustomerBankingRelationship" cbr
   WHERE "CustomerBankingRelationshipKey" = 'd94fbf63-fff5-4523-ba1c-9f12dae2c600')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "CustomerBankingRelationshipId" = EXCLUDED."CustomerBankingRelationshipId",
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey",
  "ContractKey" = EXCLUDED."ContractKey";

-- Resolve Account → Contract
INSERT INTO "Lending"."Contract" ("AccountId", "AccountKey", "ContractKey")
  (SELECT a."Id", a."AccountKey", '76dea764-8c7f-474d-98bd-6833d0f92fb5'
   FROM "Account"."Account" a
   WHERE "AccountKey" = 'b14ed96f-466e-4176-a7d2-66f9088ac384')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "AccountId" = EXCLUDED."AccountId",
  "AccountKey" = EXCLUDED."AccountKey",
  "ContractKey" = EXCLUDED."ContractKey";

-- Resolve Contract → Transaction
INSERT INTO "Lending"."Transaction" ("ContractId", "ContractKey", "TransactionKey")
  (SELECT c."Id", c."ContractKey", '9875df6c-42c3-4630-b944-c221de665a66'
   FROM "Lending"."Contract" c
   WHERE "ContractKey" = '76dea764-8c7f-474d-98bd-6833d0f92fb5')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "ContractId" = EXCLUDED."ContractId",
  "ContractKey" = EXCLUDED."ContractKey",
  "TransactionKey" = EXCLUDED."TransactionKey";

-- Resolve Account → Transaction
INSERT INTO "Lending"."Transaction" ("AccountId", "AccountKey", "TransactionKey")
  (SELECT a."Id", a."AccountKey", '9875df6c-42c3-4630-b944-c221de665a66'
   FROM "Account"."Account" a
   WHERE "AccountKey" = 'b14ed96f-466e-4176-a7d2-66f9088ac384')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "AccountId" = EXCLUDED."AccountId",
  "AccountKey" = EXCLUDED."AccountKey",
  "TransactionKey" = EXCLUDED."TransactionKey";
```

#### Phase 3 — Single batched SELECT (entire graph, 1 round trip)

```sql
SELECT
  Customer."CustomerKey",
  Customer."CustomerType",
  Customer."FirstName",
  Customer."FullName",
  Customer."LastName",
  CBR."Id"                                    AS "Id____",
  CBR."CustomerId"                            AS "CustomerId____",
  Contract."Id"                               AS "Id_____",
  Contract."CustomerBankingRelationshipId"    AS "CustomerBankingRelationshipId_____",
  Contract."ContractKey"                      AS "ContractKey_____",
  Contract."Amount"                           AS "Amount_____",
  Contract."AccountId"                        AS "AccountId_____",
  Account."Id"                                AS "Id______",
  Account."AccountName"                       AS "AccountName______",
  Account."AccountNumber"                     AS "AccountNumber______",
  Transaction."Id"                            AS "Id_______",
  Transaction."ContractId"                    AS "ContractId_______",
  Transaction."AccountId"                     AS "AccountId_______",
  Transaction."Balance"                       AS "Balance_______"

FROM "Banking"."Customer" Customer

LEFT JOIN (
  SELECT CBR."Id", CBR."CustomerId",
         Contract."Id"                            AS "Id_____",
         Contract."CustomerBankingRelationshipId" AS "CustomerBankingRelationshipId_____",
         Contract."ContractKey"                   AS "ContractKey_____",
         Contract."Amount"                        AS "Amount_____",
         Contract."AccountId"                     AS "AccountId_____",
         Account."Id"                             AS "Id______",
         Account."AccountName"                    AS "AccountName______",
         Account."AccountNumber"                  AS "AccountNumber______",
         Transaction."Id"                         AS "Id_______",
         Transaction."ContractId"                 AS "ContractId_______",
         Transaction."AccountId"                  AS "AccountId_______",
         Transaction."Balance"                    AS "Balance_______"
  FROM "Banking"."CustomerBankingRelationship" CBR
  JOIN (
    SELECT Contract."Id", Contract."CustomerBankingRelationshipId",
           Contract."ContractKey", Contract."Amount", Contract."AccountId",
           Account."Id"            AS "Id______",
           Account."AccountName"   AS "AccountName______",
           Account."AccountNumber" AS "AccountNumber______",
           Transaction."Id"        AS "Id_______",
           Transaction."ContractId" AS "ContractId_______",
           Transaction."AccountId" AS "AccountId_______",
           Transaction."Balance"   AS "Balance_______"
    FROM "Lending"."Contract" Contract
    JOIN (
      SELECT Account."Id", Account."AccountName", Account."AccountNumber",
             Transaction."Id"          AS "Id_______",
             Transaction."ContractId"  AS "ContractId_______",
             Transaction."AccountId"   AS "AccountId_______",
             Transaction."Balance"     AS "Balance_______"
      FROM "Account"."Account" Account
      JOIN (
        SELECT Transaction."Id", Transaction."ContractId",
               Transaction."AccountId", Transaction."Balance"
        FROM "Lending"."Transaction" Transaction
      ) Transaction ON Account."Id" = Transaction."AccountId_______"
    ) Account ON Contract."AccountId" = Account."Id______"
  ) Contract ON CBR."Id" = Contract."CustomerBankingRelationshipId_____"
) CBR ON Customer."Id" = CBR."CustomerId"

WHERE (Customer."CustomerKey" = '23e8761f-6373-434c-90fb-5359ffa93ff7');
```

**Join depth:** 5 tables · 4 JOIN levels · 3 schemas (`Banking`, `Lending`, `Account`) · 1 round trip

### Entity-to-Model Mapping

After the SELECT returns, `QueryHandler.MappingConfiguration` groups rows by root entity key, deduplicates, then calls `Mapper.MapByAlias` for each alias. Because `BulkMapper.Compile` ran at startup, every property read and write goes through a pre-compiled `Expression` delegate — no `Type.GetProperty` or `PropertyInfo.GetValue` calls occur at this stage.

---

## Test 2 — Three Customers (`in` filter, batch)

**Scenario:** Three customers each with one product per dataset. All three upserted and queried in a single GraphQL operation.

| Metric              | Value  |
|---------------------|--------|
| Datasets            | 5      |
| Iterations Executed | 5      |
| Iterations Failed   | 0      |
| Assertions Executed | 30     |
| Assertions Failed   | 0      |
| Pass Rate           | 100%   |
| Total Duration      | 239 ms |
| Max Response Time   | 78 ms  |
| Avg Response Time   | 16 ms  |

**Per-dataset response times:**

| Dataset   | Response Time |
|-----------|---------------|
| Dataset-1 | 14 ms         |
| Dataset-2 | 20 ms         |
| Dataset-3 | 17 ms         |
| Dataset-4 | 14 ms         |
| Dataset-5 | 13 ms         |

### GraphQL Request

```graphql
mutation a {
  wrapper(
    wrapper: {
      model: INNER_CUSTOMER
      cacheKey: "2c0c7698-465f-4fbb-a8c1-9614f7ec6c05"
      customerCustomerEdge: [
        {
          innerCustomer: {
            customerKey: "{{CustomerKey1}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming1}}"
            fullNaming: "{{FullNaming1}}"
            lastNaming: "{{LastNaming1}}"
            product: [
              {
                accountKey: "{{AccountKey1}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey1}}"
                transactionKey: "{{TransactionKey1}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey1}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        },
        {
          innerCustomer: {
            customerKey: "{{CustomerKey2}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming2}}"
            fullNaming: "{{FullNaming2}}"
            lastNaming: "{{LastNaming2}}"
            product: [
              {
                accountKey: "{{AccountKey2}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey2}}"
                transactionKey: "{{TransactionKey2}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey2}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        },
        {
          innerCustomer: {
            customerKey: "{{CustomerKey3}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming3}}"
            fullNaming: "{{FullNaming3}}"
            lastNaming: "{{LastNaming3}}"
            product: [
              {
                accountKey: "{{AccountKey3}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey3}}"
                transactionKey: "{{TransactionKey3}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey3}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey3}}"
              }
            ]
          }
        }
      ]
    }
    where: {
      customerCustomerEdge: {
        some: {
          innerCustomer: {
            customerKey: {
              in: ["{{CustomerKey1}}", "{{CustomerKey2}}", "{{CustomerKey3}}"]
            }
          }
        }
      }
    }
  ) {
    edges {
      node {
        customerCustomerEdge {
          innerCustomer {
            customerKey
            customerType
            firstNaming
            fullNaming
            lastNaming
            product {
              contractKey
              accountName
              accountNumber
              amount
              balance
            }
          }
        }
      }
    }
  }
}
```

### Generated SQL

The three-customer mutation scales the same execution pattern: **30 upsert statements** (10 per customer) followed by the same **1 SELECT** structure with a `WHERE ... IN (...)` clause. The JOIN shape is identical to Test 1 — only the filter changes.

```sql
WHERE (Customer."CustomerKey" IN (
  '{{CustomerKey1}}',
  '{{CustomerKey2}}',
  '{{CustomerKey3}}'
))
```

3× the entities, 3× the upserts, **same single SELECT round trip**. The mapping layer processes 3× the rows using the same pre-compiled delegates with no additional warmup cost.

---

## Observations

- The `Product` model spans **4 physical tables** across 3 PostgreSQL schemas. In a resolver-chain GraphQL implementation this relationship alone would trigger N+1 queries at every nesting level. Coffee Beanery compiles the entire graph into **1 SELECT** regardless of entity count or depth.
- All property access in the mapping layer uses **pre-compiled `Expression` delegates** (populated by `BulkMapper.Compile` at startup), eliminating reflection overhead from the hot path entirely.
- Scaling from 1 to 3 customers (3× entities, 3× upserts, 3× assertions) added only **3 ms** to average response time (13 ms → 16 ms). Total end-to-end duration remained **identical at 239 ms**.
- Max response time increased by only **11 ms** (67 ms → 78 ms) when handling 3× the data.
- **0 assertion failures** across all 40 assertions (10 + 30) on fully randomized UUID data.

---

## Environment Notes

- No application-level or HTTP caching active during tests
- PostgreSQL built-in execution plan cache active (plans reused after first execution)
- Mapping warmup (property cache + delegate compilation + NodeTree generation) runs once at startup before any request is served
- All keys (customer, account, contract, transaction, banking relationship) are random UUIDs per dataset
- All name fields (first, full, last) are random strings per dataset
- Schemas involved: `Banking`, `Lending`, `Account`
- Relationship traversal depth: `Wrapper → CustomerCustomerEdge → InnerCustomer → Product (Contract + Account + Transaction)`