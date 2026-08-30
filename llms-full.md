# Foundgine — AI context

Foundgine is a semantic execution layer for .NET.

Its purpose is to create a stable boundary between structured application intent and physical execution.

## Core model

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Resolution + Validation
  ↓
Authorization
  ↓
Provider-independent Plan
  ↓
Execution
  ↓
Provider
  ↓
Result + Evidence
```

## Core concepts

**Semantic model** describes application-facing entities, fields, identities, relationships, aliases, capabilities, and logical traversals.

**Intent** describes what the caller wants without directly naming SQL operations.

**Authorization** determines what the current execution context may exercise.

**Plan** describes logical execution independently of SQL/provider details.

**Provider** performs physical execution.

**Evidence** describes what was planned/executed and relevant execution/security context.

## Package architecture

`Foundgine.Abstractions` contains stable cross-layer identifiers and contracts.

`Foundgine.Metadata` describes structural metadata such as entities, fields, columns, keys, and direct relationships.

`Foundgine.Semantics` owns semantic meaning, open intent, resolution, validation, authorization, security context, logical traversal, and mutation semantics.

`Foundgine.Planning` creates provider-independent read/mutation plans and applies conservative security-preserving rewrites.

`Foundgine.Execution` provides ExecutionIR, provider compilation/execution contracts, result materialization, evidence, security conformance, and mutation execution coordination.

`Foundgine.Sql` lowers plans to parameterized SQL and implements PostgreSQL-specific query/retrieval/mutation functionality.

`Foundgine.InMemory` executes a deliberately limited subset over CLR-backed rows to prove provider independence.

`Foundgine.Aot` and `Foundgine.Aot.Generator` provide compile-time metadata declarations and generation.

`Foundgine.Intent.Json` translates structured JSON into semantic read intent.

`Foundgine.GraphQL.HotChocolate*` translates GraphQL and provides separate secure query/mutation execution boundaries.

`Foundgine.MCP` exposes semantic capabilities and intent through MCP.

`Foundgine.AI` exposes Foundgine as Microsoft.Extensions.AI tools and provides a bounded function-calling helper.

`Foundgine.Security.Authority` contains optional authority/recovery control-plane infrastructure and is outside the core semantic execution path.

## Open intent

Foundgine supports both typed and dynamic intent.

Typed:

```csharp
foundgine
    .Query<Customer>()
    .Select(c => new { c.Id, c.Name });
```

Dynamic:

```csharp
foundgine
    .Query("Customer")
    .Select("Id", "Name");
```

Both become semantic intent and use the same resolution/authorization/planning/execution pipeline.

## Logical traversals

A logical traversal may hide intermediate relationships:

```text
Customer
  → CustomerRelationship
  → Contract
  → Transaction
```

and expose:

```text
Customer.transactions
```

Resolution expands the path before authorization.

Therefore a denied intermediate entity/relationship cannot be bypassed by a logical shortcut.

## Authorization

Authorization can apply to:

- entities;
- fields;
- relationships;
- read/write operations;
- conditional predicates.

Conditional predicates remain part of execution semantics and can be lowered by providers.

Capability discovery is advisory.

## AI boundary

AI is an untrusted producer of intent.

Preferred:

```text
AI
 ↓
semantic intent
 ↓
Foundgine
 ↓
authorization
 ↓
plan
 ↓
provider
```

Avoid:

```text
LLM
 ↓
generated SQL
 ↓
database credentials
```

The host owns identity, tenant, audience, authority, secrets, model credentials, rate limits, and application policy.

## MCP boundary

MCP is a transport adapter.

The host provides trusted security context. Tool arguments must not be treated as authority.

Read capability discovery and query execution converge on Foundgine. Optional mutation tools use the same canonical mutation execution boundary.

## GraphQL boundary

GraphQL is a syntax adapter.

The secure execution packages require a host-owned security execution context and route requests through Foundgine's normal execution boundary.

## Provider security

Providers can declare/evaluate the security invariants they preserve.

Execution should proceed only when required invariants are satisfied.

Unknown or missing guarantees must fail closed.

## Current scope

The repository currently demonstrates:

- semantic modelling and resolution;
- open typed/dynamic intent;
- semantic authorization;
- logical traversals;
- provider-independent query and mutation planning;
- execution IR;
- security-preserving rewrites;
- SQL/PostgreSQL execution;
- InMemory execution;
- AOT metadata generation;
- JSON intent;
- GraphQL adapters;
- MCP integration;
- Microsoft.Extensions.AI integration;
- execution evidence;
- PostgreSQL integration tests.

Do not infer from these capabilities that Foundgine is a universal provider, autonomous-agent framework, workflow engine, ORM replacement, or authorization server.

## Current release line

1.1.9, targeting .NET 9.

When documentation and code disagree, active source and tests win.
