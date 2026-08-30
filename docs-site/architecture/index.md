# Foundgine Architecture

## Core pipeline

```text
Intent → Resolve → Authorize → Plan → Rewrite/Optimize → Provider Compilation → Execution → Result + Evidence
```

## Semantic model

Application-facing meaning: entities, fields, relationships, capabilities and authorization. It is provider-independent.

## Metadata

Structural facts can be discovered from application declarations and AOT-generated metadata without making semantic code depend on runtime reflection.

## Planning

`Foundgine.Planning` produces provider-independent plans and `ExecutionIR`. Logical filters, ordering, pagination, traversal and aggregation stay logical; physical execution choices belong to providers.

## Execution

`Foundgine.Execution` is the provider boundary for compilation/dispatch, security conformance, materialization and execution evidence.

## Providers

`Foundgine.Sql` provides SQL/PostgreSQL execution. `Foundgine.InMemory` provides a small non-SQL implementation for provider-independence testing.

## Adapters

JSON, GraphQL, MCP and AI integrations translate caller requests into Foundgine operations. They do not become the authority over execution.
