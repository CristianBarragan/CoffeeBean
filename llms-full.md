# Foundgine — Full AI Context

## Identity

Foundgine is a **.NET application-domain semantic and execution platform for AI-native applications**.

Canonical statement:

> **Foundgine turns an application's domain model into a safe, executable interface for AI agents.**

## Product thesis

Applications already contain authoritative knowledge:

```text
Entities
Identities
Relationships
Searchable fields
Actions
Policies
Storage
Execution rules
```

An LLM can reason about language, but it should not become the authority for those facts.

Foundgine therefore provides:

```text
Application domain
       ↓
Semantic model
       ↓
Structured intent
       ↓
Identity resolution
       ↓
Collection-aware traversal
       ↓
QueryIntent
       ↓
Execution plan
       ↓
Provider execution
       ↓
Verification / evidence
```

## AI boundary

Foundgine does not parse arbitrary natural language as a core responsibility.

The intended boundary is:

```text
User
 ↓
LLM / parser / application
 ↓
ReadIntent / action intent
 ↓
Foundgine
```

This keeps the execution layer deterministic and model-independent.

## Semantic model

`Foundgine.Semantic` currently contains:

```text
SemanticModel
SemanticEntity
SemanticField
SemanticIdentity
SemanticRelationship
RelationshipCardinality
SearchCapability
EntityResolver
ReadIntent
ReadPlanner
ResolvedReadPlan
```

It also contains experimental/future-facing action and policy descriptors.

The semantic model is protocol-neutral. It must not depend on:

- SQL;
- SQLite;
- GraphQL;
- Hot Chocolate;
- an LLM provider;
- MCP.

## Inference

Semantic inference exists to reduce duplicate configuration.

Existing metadata should provide what it already knows:

```text
identity
fields
relationships
storage/type facts
```

Semantic configuration should mainly add meaning that cannot safely be inferred, such as search behavior, aliases and human-facing descriptions.

## Resolution

`EntityResolver` supports:

```text
ResolveByIdentity
ResolveBySearch
ResolveByRelationship
```

Results are:

```text
Resolved
NotFound
Ambiguous
```

with evidence.

The invariant is:

> **Never silently invent an identity.**

### Resolution versus traversal

Resolution identifies one concrete object:

```text
"Ada Lovelace" → Customer #1
```

Traversal can represent a set:

```text
Customer #1
  → Accounts*
  → Transactions*
```

A one-to-many relationship must not be collapsed into one arbitrary child simply because a sample currently contains one row.

## Structured read intent

`ReadIntent` represents a request such as:

> Find Ada Lovelace's last five transactions.

without containing natural-language parsing logic.

Conceptually:

```text
Anchor:
    Customer / "Ada Lovelace"

Path:
    Accounts → Transactions

Order:
    Transaction.Id DESC

Limit:
    5
```

## Current semantic → execution proof

A real E2E test performs:

```text
ReadIntent
 ↓
EntityResolver / ReadPlanner
 ↓
ResolvedReadPlan
 ↓
QueryIntent
 ↓
QueryPlanner
 ↓
QueryPlan
 ↓
SqlPlanCompiler
 ↓
ProviderPlan
 ↓
SqlExecutionProvider
 ↓
SQLite
 ↓
ExecutionRow
```

The five-entity semantic proof additionally exercises:

```text
Customer
→ CustomerBankingRelationship
→ Contract
→ Account
→ Transaction
```

and a repeated/self-joined `Customer` occurrence.

The final `ResolvedReadPlan → QueryIntent` conversion is still assembled in the acceptance path. Therefore the architecture is E2E-proven, but the semantic-to-query bridge is not yet a frozen reusable runtime API.

## Metadata

`Foundgine.Metadata` contains the execution source of truth:

```text
EntityMetadata
ColumnMetadata
JoinMetadata
JoinGraph
ModelMetadata
```

The planner discovers relationships from metadata rather than hardcoded domain cases.

## Logical planning

`Foundgine.Planning` consumes structured `QueryIntent` and produces `Foundgine.Builders.QueryPlan`.

There is one logical planner.

Semantic translation feeds it:

```text
Semantic
  ↓
QueryIntent
  ↓
QueryPlanner
```

The semantic layer does not create a parallel physical planner hierarchy.

## Execution

`Foundgine.Execution.Contracts` defines provider-neutral contracts:

```text
ProviderPlan
ProviderNode
IExecutionProvider
ExecutionContext
ExecutionResult
ExecutionRow
ExecutionStatistics
```

`ExecutionRow` is occurrence-aware so repeated entities are not collapsed.

## SQL provider

`Foundgine.Providers` currently contains the active SQL path:

```text
SqlPlanCompiler
SqlTextTranslator
SqlExecutionProvider
```

The canonical proof uses Microsoft.Data.Sqlite.

## Proven E2E coverage

The active test suite covers:

```text
Linear Customer → Account → Transaction
Branching query tree
No invented relationship
Ugly physical schema
Five-entity composite
Repeated/self-joined entity
Filter/sort/page
Create/update/delete
Atomic multi-entity mutation plan
Semantic resolution
Structured read intent
Read intent → real SQLite
Composite semantic/read proof
```

## Mutation status

Low-level mutation planning and SQLite execution support:

- Create;
- Update;
- Delete.

The planner rejects an unfiltered Update.

The semantic action/policy lifecycle is still future work.

## Architecture

Active project references are:

```text
Foundation → Abstractions
Metadata → Foundation
Diagnostics → Foundation
Builders → Metadata
Execution.Contracts → Metadata
Semantic → Metadata
Planning → Metadata + Builders
Providers → Builders + Execution.Contracts
```

Important boundaries:

```text
Semantic does not know SQL.
Planning does not know LLMs.
Execution.Contracts does not know provider implementations.
Providers do not redefine domain semantics.
```

## Product roadmap

### Current

1. Reusable semantic → query bridge.
2. Collection-aware traversal proof.
3. Benchmark the pipeline.
4. Simplify semantic configuration.

### Next

5. Domain actions.
6. Policy/authorization.
7. Preview/approval.
8. Verification/evidence.

### Later

9. MCP adapter.
10. Additional execution targets.
11. Roslyn semantic compiler.

## Roslyn direction

A future compiler can derive:

```text
Stable IDs
Entity descriptors
Relationship descriptors
Search descriptors
Action descriptors
Policy metadata
Planner hints
```

It should not generate fixed plans for future natural-language requests.

## MCP

MCP is a future outer adapter:

```text
Claude / ChatGPT / Cursor
        ↓
       MCP
        ↓
Foundgine Semantic API
```

MCP is not the Foundgine core.

## Competitive positioning

Foundgine is complementary to:

- EF Core;
- Dapper;
- Semantic Kernel;
- LangChain/LangGraph;
- MCP;
- Temporal;
- Kafka;
- vector stores;
- GraphQL.

It does not need to replace those technologies.

Its differentiator is the **application-domain semantic/execution boundary** and the ability to bridge a rich logical/domain model to a dynamic physical execution plan.

## Historical material

`archive/` contains historical Graphgine/GraphQL/Hot Chocolate work, earlier generators and older prototypes.

Historical material must not be presented as active Foundgine functionality.

## Accuracy rules

Do not claim:

- production autonomous-agent support;
- complete MCP support;
- complete RAG;
- universal provider support;
- benchmark superiority;
- formal Native AOT compatibility;
- production authorization/verification;

unless active code and tests prove the claim.
