# Layers

[Home](../../README.md) → [Architecture](README.md) → **Layers**

## Layer 1 — Domain/application

Owned by the application.

Examples:

```text
Customer
Account
Transaction
IssueRefund()
```

Foundgine should avoid forcing developers to duplicate this knowledge.

## Layer 2 — Metadata

`Foundgine.Metadata`

Describes executable structure:

```text
Entity
Column
Field
Relationship
Join
Model
Graph
```

Metadata is the source of truth for physical storage facts and join structure.

## Layer 3 — Semantic model

`Foundgine.Semantic`

Describes application meaning:

```text
SemanticEntity
Identity
Field
Relationship
RelationshipCardinality
SearchCapability
```

It is protocol-neutral and must not know SQL or providers.

## Layer 4 — Intent and resolution

Structured intent identifies what the caller wants.

Resolution turns ambiguous references into explicit identities.

```text
"Ada Lovelace"
       ↓
Customer #1
```

A key rule is that **resolution is identity-oriented while traversal is set-oriented**.

For example:

```text
Customer #1
   ↓ 1:N
Accounts
   ↓ 1:N
Transactions
```

The relationship traversal should be represented in the query intent rather than pretending there is only one Account or Transaction.

## Layer 5 — Planning

`Foundgine.Planning`

Transforms structured execution intent into provider-neutral logical plans.

There is one logical planner. Semantic translation feeds it; semantic code does not replace it.

## Layer 6 — Execution contracts

`Foundgine.Execution.Contracts`

Defines provider-independent execution concepts:

```text
ProviderPlan
ProviderNode
ExecutionRow
ExecutionResult
ExecutionStatistics
IExecutionProvider
```

## Layer 7 — Provider

`Foundgine.Providers`

Turns logical plans into provider-specific plans and executes them.

Current real proof:

```text
SQLite
```

## Layer 8 — External adapters

Future integrations may include:

```text
MCP
ASP.NET Core
GraphQL
REST
gRPC
LLM clients
```

These stay outside the core.
