# The Foundgine Flagship Proof

This is the smallest repository example intended to answer: **what is Foundgine?**

A JSON producer describes semantic intent. Foundgine then resolves that intent against a domain model, applies authorization, creates one provider-independent execution plan, and lets different providers execute that plan.

```text
JSON / agent intent
        |
        v
 Semantic resolution
        |
        v
 Authorization
        |
        v
 ExecutionPlan
        |
        +-------------------+
        |                   |
        v                   v
      SQL             InMemory / CLR
        |                   |
        +---------+---------+
                  v
          equivalent result
```

## What the proof establishes

The test `FlagshipProofTests.One_semantic_intent_crosses_authorization_planning_and_two_providers` deliberately keeps the request small:

- root entity: `Customer`
- fields: `Id`, `Name`
- filter: `Name == "Alice"`
- order: `Name ASC`
- limit: `1`

The request is parsed from JSON, but JSON is only a producer. The adapter does not execute anything.

The resolved request is authorized using a semantic policy. The planner then creates the same logical `ExecutionPlan` used by both providers.

### SQL provider

The SQL compiler lowers the logical plan into SQL and executes it against SQLite.

### In-memory provider

The in-memory compiler retains the logical plan and executes it directly over CLR-backed rows. It does not generate SQL.

Both return `Alice`.

## Why this matters

The proof is intentionally not a performance benchmark. It demonstrates a boundary:

> **Meaning is decided before provider execution.**

That means an agent, GraphQL adapter, JSON API, or another producer can describe intent without becoming responsible for storage access, authorization enforcement, or provider-specific execution.

The provider decides **how** to execute the already-authorized meaning.

## What this proof does not claim

It does not claim that every current Foundgine feature is supported equally by every provider. The in-memory provider is intentionally small. The point is that the provider can be different while consuming the same logical plan.

It also does not claim that JSON itself is an agent protocol. JSON is simply a convenient structured producer for the proof.
