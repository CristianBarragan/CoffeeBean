# Foundgine — AI context

Foundgine is a semantic execution layer for .NET.

It separates:

```text
what the caller wants
```

from:

```text
how a provider executes it
```

## Core flow

```text
Request
 ↓
Resolve
 ↓
Authorize
 ↓
Plan
 ↓
Provider
 ↓
Result
```

## Vocabulary

**Model**: what the application exposes.

**Request**: what the caller wants.

**Authorization**: what the caller may do.

**Plan**: a provider-independent description of the work.

**Provider**: the physical executor.

**Result**: returned data and execution evidence.

## Architecture

Input adapters include JSON and GraphQL. AI and application code can also create structured requests.

The semantic core owns meaning, resolution, authorization, planning, execution contracts, and result handling.

Providers own physical execution.

The core must not depend on a transport or provider.

## Current projects

`Foundgine.Abstractions` contains stable IDs and small contracts.

`Foundgine.Metadata` describes application and storage metadata.

`Foundgine.Semantics` owns semantic meaning, request resolution, authorization, and capability discovery.

`Foundgine.Planning` creates provider-independent plans.

`Foundgine.Execution` defines provider execution contracts and result materialization.

`Foundgine.Sql` is the SQL provider.

`Foundgine.InMemory` is a deliberately small non-SQL provider used to test provider independence.

`Foundgine.Intent.Json` and `Foundgine.GraphQL.HotChocolate*` are input adapters.

`Foundgine.Aot` and `Foundgine.Aot.Generator` provide compile-time metadata.

## AI boundary

AI is an input source, not the authority.

An AI system can ask for:

```text
Customer
 ├── name
 └── orders
```

Foundgine decides whether that request is valid and authorized, then builds the plan.

Capability discovery is descriptive. It does not grant permission.

## Current proof

The active tests cover semantic modelling, resolution, authorization, provider-independent query and mutation planning, SQL/SQLite execution, a small InMemory provider, AOT metadata, JSON input, GraphQL adapters, relationships, aggregates, pagination, and PostgreSQL integration.

Historical material is under `docs/history`.

When documentation and code disagree, current code and active tests win.
