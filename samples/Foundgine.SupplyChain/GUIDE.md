# Foundgine Supply Chain — Layer-by-Layer Guide

This guide explains the sample as an application architecture, not as a benchmark script.

## 1. API layer — `Api`

`Api/Program.cs` is deliberately small. It creates the ASP.NET host, registers the application/infrastructure composition roots, enables the Foundgine MCP adapter, and maps `/mcp` and health endpoints.

The API does **not** contain SQL, business rules, semantic definitions or authorization policy.

```text
MCP request
    ↓
SupplyChainMcpTools
    ↓
SupplyChainApplication
```

## 2. Application layer — `Application`

The application layer defines the use-case boundary through `ISupplyChainQueries` and `ISupplyChainMutations`.

`SupplyChainApplication` performs capability authorization before delegating to the appropriate port.

This gives us:

```text
protocol
   ↓
application capability
   ↓
use-case contract
   ↓
provider implementation
```

Changing MCP to another transport does not require changing the use cases.

## 3. Domain layer — `Domain`

The domain project contains two intentionally different concepts.

### Storage records

`*ERP` types describe physical storage-facing entities and are decorated with `FoundgineEntity`/`FoundgineField`/`FoundgineRelationship`.

Examples:

- `CustomerERP`
- `SalesOrderERP`
- `CatalogProductERP`
- `InventoryPositionERP`

Their attributes specify both semantic names and physical names.

For example:

```text
SalesOrder
    storage name: orders

SalesOrder.Id
    storage column: order_id
```

### Application models

The model declarations use model-focused names rather than database names.

```text
Customer
SalesOrder
SalesOrderLine
CatalogProduct
InventoryPosition
```

This keeps the semantic vocabulary stable if the physical schema changes.

## 4. AOT layer

`Foundgine.Aot` attributes are compiled by `Foundgine.Aot.Generator`.

The generator emits `Foundgine.Generated.GeneratedMetadata`.

The sample consumes the generated registry through `SupplyChainSemanticModel.Metadata`.

```text
AOT declarations
      ↓
Foundgine.Aot.Generator
      ↓
GeneratedMetadata
      ↓
IMetadataProvider
      ↓
Planner / SqlCompiler
```

The important architectural point is that runtime does not need to rediscover the storage metadata graph.

## 5. Semantic configuration — `Application/SupplyChainSemanticConfiguration.cs`

`SupplyChainSemanticModel` contains stable semantic IDs for entities and relationships.

These IDs are used by semantic operations rather than database table names.

For example:

```text
CatalogProduct
InventoryPosition
SalesOrder
SalesOrderLine
```

are semantic concepts, while PostgreSQL knows about `products`, `inventory`, `orders`, and `order_items`.

This is the boundary that makes the repository future-proof.

## 6. Query repository — `Infrastructure/Queries`

A query repository creates a semantic operation.

Example flow:

```text
GetOrders(customerId)
       ↓
SemanticReadNode(SalesOrder)
       ↓
GeneratedSemanticModel.SalesOrder.CustomerId.Eq(customerId)
       ↓
Foundgine Planner
       ↓
Execution plan
       ↓
Foundgine.Sql.SqlCompiler
       ↓
SqlPlan
       ↓
SqlExecutionProvider
       ↓
PostgreSQL
```

There is no repository-level SQL string for the normal query path.

If PostgreSQL is replaced later, the semantic application code does not need to change.

## 7. Mutation repository — `Infrastructure/Mutations`

Simple mutations follow the same semantic path.

For example `UpdateInventory` becomes:

```text
SemanticMutationBuilder.Update
       ↓
MutationPlanner
       ↓
SqlMutationCompiler
       ↓
SqlMutationExecutionProvider
       ↓
PostgreSQL
```

This is the preferred path for future mutations that can be represented by Foundgine's mutation IR.

## 8. High-assurance mutations

`PlaceOrder` and `CancelOrder` are intentionally different.

They contain invariants that are currently PostgreSQL-specific:

- idempotency/replay protection
- advisory transaction locking
- `FOR UPDATE SKIP LOCKED`
- inventory reservation races
- atomic order + allocation + inventory changes
- cancellation inventory restoration

The sample therefore keeps explicit parameterized SQL for these orchestration paths rather than pretending that a generic repository abstraction makes those invariants disappear.

The long-term direction is to progressively move expressible portions into Foundgine's mutation IR while retaining provider-specific transactional primitives where they are genuinely required.

## 9. MCP layer

`SupplyChainMcpTools` contains only protocol adapters.

A tool such as `get_order` does not know how an order is stored or queried. It invokes the application capability.

That makes the MCP surface replaceable and keeps the semantic application architecture transport-independent.

## 10. Testing layer

`Tests` is the seam for validating each layer independently.

Recommended progression:

1. capability authorization tests
2. AOT metadata tests
3. semantic plan tests
4. SQL compilation tests
5. PostgreSQL integration tests
6. MCP contract tests
7. full agent-facing E2E benchmark tests

The existing `benchmarks/AgentEndToEnd/SupplyChain` remains untouched and continues to provide the benchmark workload and comparison harness.

## 11. Why this structure is future-proof

The key dependency rule is:

```text
API
 ↓
Application
 ↓
Semantic intent
 ↓
Foundgine planning
 ↓
Provider
```

Not:

```text
API
 ↓
SQL repository
 ↓
PostgreSQL
```

That means future changes such as:

- another MCP transport
- another database provider
- different SQL dialect
- richer authorization
- generated capability metadata
- additional agent-facing operations
- alternative execution providers

can be introduced at the appropriate boundary rather than forcing a rewrite of the application.

## 12. Relationship to the benchmark

There are now two deliberately separate artifacts:

```text
benchmarks/AgentEndToEnd/SupplyChain
    = benchmark harness / stable comparison workload

samples/Foundgine.SupplyChain
    = maintainable reference application / architecture sample
```

The benchmark can therefore remain stable while this sample evolves as Foundgine's recommended application architecture evolves.