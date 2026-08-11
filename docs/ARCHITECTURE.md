# Architecture

Foundgine separates domain meaning from physical execution.

```text
Input
  ↓
Semantic Request
  ↓
Resolve → Authorize
  ↓
Semantic Graph
  ↓
Execution Plan
  ↓
Provider Plan
  ↓
Data source
```

## Boundaries

**Semantics** knows the domain model and request meaning. It does not know SQL or GraphQL.

**Planning** turns an authorized semantic graph into logical operations such as scans and relationship traversals. It does not know tables or SQL.

**Execution** owns the provider boundary and result materialization.

**Providers** turn logical plans into physical work.

**Adapters** translate external protocols into Foundgine requests. They do not become part of the core.

## Dependency direction

```text
Abstractions
    ↑
Metadata ← AOT
    ↑
Semantics
    ↑
Planning
    ↑
Execution
    ↑
Providers

Adapters sit at the edge and depend on the contracts they translate.
```

The exact project references are enforced by the solution and architecture tests.
