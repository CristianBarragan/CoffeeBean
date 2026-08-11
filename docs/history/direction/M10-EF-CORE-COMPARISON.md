# M10 — Foundgine vs EF Core + Hot Chocolate

## Purpose

This is a design-level comparison, not a runtime benchmark. The goal is to determine where the Foundgine semantic execution layer earns its complexity and where a conventional EF Core + application-policy implementation is clearly simpler.

## Representative requirement

> Find Alice's five most recent transactions, following `Transaction → Account → Customer`, while applying authorization before execution.

### Foundgine

```text
ReadIntent
  ↓
ReadIntentCompiler
  ↓
SemanticRequest
  ↓
Resolution
  ↓
Authorization
  ↓
ExecutionPlan
  ↓
SQL provider
```

The producer supplies semantic concepts and does not need SQL, table names, joins, or ORM expression trees.

### Conventional EF Core

A conventional implementation can be approximately:

```csharp
var query = db.Transactions
    .Where(t => t.Account.Customer.Name == "Alice")
    .OrderByDescending(t => t.TransactionDate)
    .Take(5);

query = ApplyApplicationPolicy(user, query);

var rows = await query
    .Select(t => new TransactionDto
    {
        Id = t.Id,
        Amount = t.Amount,
        TransactionDate = t.TransactionDate
    })
    .ToListAsync();
```

For a fixed application query, this is dramatically simpler.

## Where EF Core wins

Foundgine should not be selected merely because it can express the query above.

EF Core is the better choice when:

- the query shapes are known at development time;
- the application has one persistence model;
- LINQ is an acceptable intent language;
- authorization can be expressed cleanly in application/query policy code;
- one provider is sufficient;
- runtime model discovery is acceptable;
- GraphQL is already handled by Hot Chocolate;
- CRUD and change tracking are important.

For this workload, Foundgine's additional projects, semantic model, resolver, planner, provider abstraction, and generated metadata are overhead.

## Where Foundgine can win

The comparison changes when the producer is not trusted to construct ORM expressions directly.

For example:

```text
LLM / API / GraphQL / workflow
              ↓
        Structured Intent
              ↓
        Foundgine validation
              ↓
        Authorization
              ↓
        Deterministic plan
              ↓
             SQL
```

The producer can request:

- entity and field selections;
- relationship traversal;
- relationship filters;
- ordering;
- limits;
- other supported semantic controls.

It cannot directly construct arbitrary SQL or arbitrary EF expression trees.

That is the actual security and determinism argument for the semantic layer.

## Complexity comparison

| Capability | EF Core + app code | Foundgine |
|---|---|---|
| Fixed LINQ query | **Excellent** | Overkill |
| CRUD/change tracking | **Excellent** | Not the goal |
| GraphQL protocol | Hot Chocolate | Adapter only |
| Dynamic semantic query | Application-specific code | **Core capability** |
| Relationship-aware dynamic paths | Custom expression builder | **Semantic model** |
| Authorization before physical planning | Possible, application-defined | **Explicit pipeline stage** |
| Shared intent producer boundary | Custom convention | **First-class** |
| Provider-independent execution plan | Not normally required | **First-class** |
| AOT/static domain metadata | Possible, but not the primary model | **First-class path** |
| LLM-generated deterministic intent | Custom guardrails required | **Natural boundary** |
| Simple application development | **Winner** | Loser |

## Complexity cost

The current source contains roughly 3,500 lines across the foundational runtime projects before considering all tests and documentation. That is material engineering cost.

The semantic intent vertical slice itself is small, but it depends on the larger runtime architecture. Therefore the right question is not whether `ReadIntent` is elegant in isolation. The question is whether multiple future producers and policies can reuse the same semantic pipeline.

## Decision rule

Use EF Core + Hot Chocolate when:

```text
known application queries
+ one provider
+ conventional authorization
+ normal CRUD
```

Use Foundgine when:

```text
dynamic intent
+ semantic relationship traversal
+ authorization over the semantic graph
+ multiple intent producers
+ deterministic execution boundary
```

Multiple providers and AOT strengthen the case, but should not be assumed merely to justify the architecture.

## What this means for Graphgine

Graphgine should be the product that exposes this substrate to applications.

Foundgine should remain the semantic execution engine rather than becoming another ORM or GraphQL framework.

```text
                  Graphgine
        ┌────────────┼────────────┐
        ↓            ↓            ↓
    GraphQL        API          AI/LLM
        \            |            /
         \           |           /
          └──── Structured Intent
                       ↓
                    Foundgine
                       ↓
             Resolve / Authorize
                       ↓
                    Plan
                       ↓
                 SQL / Provider
```

## Decision

**Foundgine is not justified as a replacement for EF Core.**

It is justified only if the application needs a reusable semantic execution boundary that sits between untrusted/dynamic intent producers and physical data execution.

That should be the project's primary product thesis going forward.

## Next test

The next useful proof is not another GraphQL feature. It is a second independent intent producer using the same semantic contracts and producing the same deterministic execution path.

A useful candidate is a small JSON intent adapter. It should translate validated JSON into `ReadIntent` without introducing JSON concepts into Semantics, Planning, or SQL.
