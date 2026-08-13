# Agent Semantic Boundary

Foundgine treats an LLM or agent as an **intent producer**, not an execution authority.

The agent should describe what it wants in the application's vocabulary. Foundgine then resolves that intent against the semantic model, applies authorization, creates the execution plan, and delegates physical execution to a provider.

## The boundary

```text
LLM / Agent
    |
    | provider-neutral semantic intent
    v
ReadIntent
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
    v
Provider
```

The agent does **not** need to know:

- SQL table names;
- SQL joins or predicates;
- ORM expression trees;
- database-specific syntax;
- authorization implementation details;
- provider APIs.

## Why this matters

Consider a request such as:

> Find Alice's five most recent transactions.

A transport-specific agent might produce SQL, a GraphQL document, or an ORM expression. Each representation couples the agent to a particular execution technology.

Foundgine's semantic representation can instead express:

```text
root: Transaction
filter:
  Account -> Customer -> Name = "Alice"
order:
  TransactionDate DESC
limit: 5
```

That representation has application meaning but no physical storage assumptions.

The same intent can then be consumed by different producers and lowered by different providers.

## What the agent is allowed to influence

The agent can propose:

- entity names;
- field selections;
- relationship traversals;
- filters;
- ordering;
- pagination.

The agent cannot grant itself:

- access to an entity;
- access to a field;
- access to a relationship;
- authorization context;
- permission to bypass semantic validation;
- permission to select a provider or inject provider instructions.

Those decisions belong to Foundgine's trusted pipeline.

## The important security property

The safe flow is:

```text
agent-generated intent
        |
        v
semantic validation
        |
        v
authorization
        |
        v
planning
        |
        v
provider execution
```

Not:

```text
agent-generated SQL -> database
```

And not:

```text
agent-generated capability snapshot -> trusted execution
```

Capability discovery remains descriptive. The actual intent is authorized again before planning.

## The repository proof

The current repository already has the primitives needed for this boundary:

1. `ReadIntent` is provider-neutral.
2. `JsonReadIntentAdapter` demonstrates that an external representation can produce it.
3. `ReadIntentCompiler` resolves semantic names without knowing SQL.
4. `SemanticAuthorizer` applies policy after resolution.
5. `Planner` creates the provider-independent execution plan.
6. SQL and in-memory providers consume the resulting plan through separate provider boundaries.
7. Existing multi-producer tests demonstrate that GraphQL and JSON can converge on the same semantic request.
8. Existing untrusted-intent tests demonstrate rejection and authorization before provider execution.

This is the boundary Foundgine should expose to future agent integrations. An LLM SDK, MCP server, or agent framework should be an adapter around this boundary rather than becoming part of Foundgine Core.
