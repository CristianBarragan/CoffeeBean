# Samples

## Foundgine.Samples.Banking

The canonical proof domain is intentionally small:

```text
Customer
   ↓
Account
   ↓
Transaction
```

The sample intentionally has no GraphQL or Hot Chocolate dependency.

## Current proof path

```text
Banking domain
   ↓
Foundgine.Metadata
   ↓
Foundgine.Semantic
   ↓
Structured ReadIntent
   ↓
Resolution
   ↓
QueryIntent
   ↓
Foundgine.Planning
   ↓
Foundgine.Providers
   ↓
SQLite
```

It demonstrates:

- metadata;
- semantic model and inference support;
- deterministic entity resolution;
- structured read intent;
- dynamic planning;
- filtering, sorting and paging;
- SQL provider execution;
- evidence-oriented output;
- the five-entity composite proof in the test suite.

## Canonical scenario

> **Find Ada Lovelace's last five transactions.**

The sentence itself is not parsed by Foundgine. The sample constructs `ReadIntent` as the structured boundary that an LLM, UI or other caller would produce.

## Important limitation

The current sample proves the semantic-to-query connection, but the final translation from `ResolvedReadPlan` to `QueryIntent` is still explicitly assembled in the sample/test path. Productizing that translation is the next core task.

The next hard proof should also give Ada multiple accounts and require the query to traverse **all** of them before ordering and limiting transactions. That tests collection cardinality rather than accidentally passing because the sample has one account.

## Run

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

## Why SQLite?

The goal is to prove Foundgine, not database deployment.

SQLite makes the sample:

- self-contained;
- deterministic;
- easy to run;
- independent of Docker/PostgreSQL.

## Tests

The broader `tests/Foundgine.Tests` suite contains the stronger E2E acceptance scenarios, including the five-entity composite and semantic/read integration proofs.
