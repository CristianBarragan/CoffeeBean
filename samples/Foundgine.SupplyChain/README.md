# Foundgine Supply Chain — Layered MCP Sample

A maintainable, agent-facing Supply Chain application showing **MCP → application → semantic model → Foundgine planning → Foundgine.Sql → PostgreSQL**.

This sample intentionally lives under `samples/` and is separate from `benchmarks/AgentEndToEnd/SupplyChain`. The existing benchmark remains the stable benchmark harness; this sample is the architecture/reference implementation to evolve independently.

## Architecture

```text
Agent / MCP client
        ↓
Api
        ↓
Application
        ↓
Domain + Semantics
        ↓
Foundgine Planning / Execution
        ↓
Foundgine.Sql
        ↓
PostgreSQL
```

### Projects

| Project | Responsibility |
|---|---|
| `Api` | ASP.NET + MCP transport; protocol adapter only |
| `Application` | use cases, capability authorization and application contracts |
| `Domain` | storage records and AOT application models |
| `Semantics` | stable semantic entity/relationship identities and generated metadata entry point |
| `Infrastructure` | PostgreSQL integration and Foundgine.Sql query/mutation adapters |
| `Tests` | application, semantic and infrastructure tests |

## Foundgine packages used

The sample uses the relevant runtime surface together rather than introducing a parallel SQL abstraction:

- `Foundgine`
- `Foundgine.Abstractions`
- `Foundgine.Aot`
- `Foundgine.Metadata`
- `Foundgine.Semantics`
- `Foundgine.Planning`
- `Foundgine.Execution`
- `Foundgine.Sql`
- `Foundgine.MCP`
- `ModelContextProtocol.AspNetCore`
- `Npgsql`

The sample uses the released Foundgine 0.5.2 NuGet packages. The AOT generator runs from the package during compilation. Numeric `FieldId` values are runtime identities; application code should use the semantic API rather than constructing FieldId values manually.

## AOT model-focused naming

Physical persistence entities are deliberately named `*ERP`, while Foundgine exposes model-focused semantic names such as:

```text
CustomerERP            → Customer
SalesOrderERP          → SalesOrder
SalesOrderLineERP  → SalesOrderLine
CatalogProductERP  → CatalogProduct
InventoryPositionERP → InventoryPosition
```

This keeps storage terminology out of the application-facing semantic vocabulary while retaining explicit physical storage names such as `orders`, `order_items`, and `inventory`.

## Dynamic SQL

Repositories do not contain handwritten SELECT statements for normal queries. They construct Foundgine semantic operations:

```text
Repository
   ↓
SemanticOperation
   ↓
Planner
   ↓
Execution plan
   ↓
SqlCompiler
   ↓
SqlPlan
   ↓
SqlExecutionProvider
   ↓
PostgreSQL
```

Simple mutations use the same path through `MutationPlanner` and `SqlMutationCompiler`.

The high-assurance `PlaceOrder` and `CancelOrder` workflows retain explicit transaction orchestration because they require PostgreSQL-specific advisory locks, `FOR UPDATE SKIP LOCKED`, idempotency, conditional inventory allocation and multi-table atomicity. This is deliberate: Foundgine handles semantic planning where the operation is expressible in its mutation IR, while the benchmark's strongest transaction invariants remain explicit at the database boundary.

## Step-by-step guide

If you are new to Foundgine, start here. This is a hands-on tutorial that walks from an empty solution through entities, models, explicit model/entity mapping, AOT generation, semantic queries, mutations, infrastructure, API and tests:

**[Build the Supply Chain example from scratch — TUTORIAL.md](TUTORIAL.md)**

For a shorter architecture walkthrough, see:

**[Foundgine Supply Chain — Layer-by-Layer Guide](GUIDE.md)**

The guide explains the solution from the MCP boundary through application contracts, AOT metadata, semantic operations, planning, SQL compilation, PostgreSQL execution, mutations, testing and future extension points.

## Run

From the repository root:

```powershell
dotnet run --project samples/Foundgine.SupplyChain/Api/Foundgine.SupplyChain.Api.csproj
```

Set `SupplyChainConnectionString` to the Supply Chain PostgreSQL database before starting the API.

For the containerized sample:

```powershell
cd samples/Foundgine.SupplyChain
docker compose up --build
```

MCP endpoint: `http://localhost:4422/mcp`

Health endpoint: `http://localhost:4422/health`

## Generated semantic API

The application-facing query and mutation code does not construct numeric `FieldId` values. The source generator resolves model properties to their mapped ERP fields and emits named handles:

```csharp
SupplyChainSemanticFields.InventoryPosition.WarehouseId.Eq(warehouseId)
SupplyChainSemanticFields.InventoryPosition.ProductId.Eq(productId)
SupplyChainSemanticFields.InventoryPosition.QuantityOnHand.Set(quantity)
SupplyChainSemanticFields.Shipment.Status.Set("In Transit")
```

The generated handles carry the compact IDs internally, but those IDs are not part of the developer-facing contract. The model-to-ERP mapping remains explicit and lives outside both model and ERP classes.


## Foundgine dependencies

This sample consumes Foundgine through its published NuGet packages rather than source-project references. The sample application projects remain project-to-project references; Foundgine is an external package dependency.

The tutorial uses Foundgine `0.5.2`. See `TUTORIAL.md` for the complete package installation commands.

## Using the repository source

This sample is intentionally wired to the Foundgine projects under `../../../src` instead of the published Foundgine NuGet packages.

This makes the sample build against the exact source tree in this repository, including the AOT source generator and current runtime changes.

If you copy the sample outside this repository, change the `ProjectReference` entries back to the published NuGet packages or update the paths to your Foundgine source checkout.
