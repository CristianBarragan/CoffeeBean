> Source content for [`index.html`](index.html), the page actually served on the site. Edit this file, then regenerate the HTML page and `llms-full.md`.

# Get started with Foundgine

Run the `Foundgine.SupplyChain` sample end to end, then walk through it layer by layer — MCP boundary, application use cases, domain and AOT metadata, semantic model, and PostgreSQL execution. This page follows [GUIDE.md](https://github.com/cristianbarragan/Foundgine/blob/main/samples/Foundgine.SupplyChain/GUIDE.md) in the sample.

## What you'll run

The sample is a small supply-chain domain exposed to agents over MCP: customers, orders, order lines, products, suppliers, categories, inventory positions, warehouses, shipments and carriers.

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

Source: `samples/Foundgine.SupplyChain` in the repository. It is deliberately separate from `benchmarks/AgentEndToEnd/SupplyChain`, which stays fixed as a benchmark workload while this sample is free to evolve as the recommended reference architecture.

## 1. Prerequisites

- .NET 9 SDK
- Docker (for PostgreSQL, and optionally to build the API image)
- A clone of the [Foundgine repository](https://github.com/cristianbarragan/Foundgine)

The sample references the Foundgine `src/` projects directly rather than the published NuGet packages, so it always builds against the exact source in the repository, including the current AOT source generator. If you copy the sample outside the repository, switch those `ProjectReference` entries to the published `Foundgine.*` NuGet packages — see `PACKAGE-COMPATIBILITY.md` in the sample folder.

## 2. Start PostgreSQL

```powershell
cd samples/Foundgine.SupplyChain
docker compose up --build
```

This starts PostgreSQL on `localhost:4429` **and** the API container on `localhost:4422`, with `SupplyChainConnectionString` already wired to the containerized database. If you want to run the API this way, skip straight to the health check below — step 3 is only needed if you'd rather run the API locally (e.g. to attach a debugger) instead of in a container.

If you only want the database, start just that service instead:

```powershell
docker compose up postgres --build
```

## 3. Run the API

Skip this step if you already ran the full `docker compose up --build` above — the API is already running on port 4422.

Otherwise, with just PostgreSQL running (`docker compose up postgres --build`), run the API locally on the same port so the rest of this guide's URLs stay correct:

```powershell
dotnet run --project samples/Foundgine.SupplyChain/Api/Foundgine.SupplyChain.Api.csproj --urls http://localhost:4422
```

Check that it's up:

```powershell
curl http://localhost:4422/health
curl http://localhost:4422/health/ready
```

The MCP endpoint is `http://localhost:4422/mcp`, exposing: `describe_capabilities`, `get_my_orders`, `get_order`, `get_shipment`, `list_products`, `list_customers`, `get_product`, `get_inventory`, `list_suppliers`, `update_inventory`, `create_shipment`, `update_shipment`, `place_order`, `cancel_order` — each a thin adapter over `SupplyChainApplication`.

Every tool call requires both an `actor` and a `token` — a caller has to prove it actually controls the identity it claims, not just name one. `SupplyChainApplication` checks capability authorization for every call before it reaches the semantic layer; see [How it works](../how-it-works/index.html) for the full authorization → planning → execution path, and the repository's `security/pentest/` suite for how that boundary is tested.

## 4. Walk the architecture layer by layer

### Layer 1 — API layer (`Api`)

`Api/Program.cs` is deliberately small: it calls `AddSupplyChainCore(connectionString)` — a bundled extension shared with the PenTest sample's GraphQL/MCP hosts that registers the application/infrastructure composition roots and the shared capability registry in one call — enables the Foundgine MCP adapter, and maps `/mcp` and health endpoints via the same file's `MapSupplyChainHealthChecks()`. No SQL, business rules, semantic definitions or authorization policy live here.

```text
MCP request
    ↓
SupplyChainMcpTools
    ↓
SupplyChainApplication
```

### Layer 2 — Application layer (`Application`)

Defines the use-case boundary through `ISupplyChainQueries` and `ISupplyChainMutations`. `SupplyChainApplication` performs capability authorization before delegating to the appropriate port. Alongside that runtime check, the `Semantics` layer declares the same capabilities' authorization requirements as descriptive `SemanticCapabilityDefinition` metadata (the Step 5/6 capability-definition API) — see Layer 5 below.

```text
protocol
   ↓
application capability
   ↓
use-case contract
   ↓
provider implementation
```

### Layer 3 — Domain layer (`Domain`)

Two intentionally different, unrelated CLR representations of the same business concepts:

- **Storage records** — `*ERP` types decorated with `FoundgineEntity`/`FoundgineField`/`FoundgineRelationship`, carrying both semantic and physical names (e.g. `SalesOrder` stores as table `orders`).
- **Application models** — `Customer`, `SalesOrder`, `SalesOrderLine`, `CatalogProduct`, `InventoryPosition` and friends, named for the business vocabulary. The model does not reference the ERP type. Model/entity mappings and connection targets live in the separate schema-bound `Domain/Mappings.cs` declarations.

### Layer 4 — AOT layer (generated metadata)

`Foundgine.Aot` attributes on the Domain types are compiled by `Foundgine.Aot.Generator`, which emits `Foundgine.Generated.GeneratedMetadata`, consumed through `SupplyChainSemanticModel.Metadata`.

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

### Layer 5 — Semantic layer (`Semantics`)

`SupplyChainSemanticModel` holds stable semantic IDs for entities and relationships, used by semantic operations instead of raw database table names. `SupplyChainCapabilities` in the same project declares each capability as a `SemanticCapabilityDefinition` with declarative authorization-requirement metadata, and builds the shared `SemanticCapabilityRegistry` every host registers.

### Layer 6 — Query repository (`Infrastructure/Queries`)

Builds a semantic operation rather than a SQL string:

```text
GetOrders(customerId)
       ↓
SemanticReadNode(SalesOrder)
       ↓
SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId)
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

### Layer 7 — Mutation repository (`Infrastructure/Mutations`)

Simple mutations follow the same semantic path:

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

### Layer 8 — High-assurance mutations

`place_order` and `cancel_order` carry invariants that are currently PostgreSQL-specific — idempotency/replay protection, advisory transaction locking, `FOR UPDATE SKIP LOCKED`, inventory reservation races, atomic order + allocation + inventory changes, and cancellation inventory restoration — so they keep explicit parameterized SQL rather than a generic repository abstraction.

### Layer 9 — MCP layer

`SupplyChainMcpTools` contains only protocol adapters — a tool like `get_order` invokes the application capability directly without knowing how an order is stored or queried.

### Layer 10 — Testing layer (`Tests`)

The seam for validating each layer independently: capability authorization, AOT metadata, semantic plans, SQL compilation, PostgreSQL integration, MCP contracts, then full agent-facing E2E benchmark tests.

**The key dependency rule:** API → Application → Semantic intent → Foundgine planning → Provider — never API → SQL repository → PostgreSQL directly.

## 5. Next steps

- **Build it from scratch.** [TUTORIAL.md](https://github.com/cristianbarragan/Foundgine/blob/main/samples/Foundgine.SupplyChain/TUTORIAL.md) starts from an empty solution and ends at this same sample.
- **Read the full layer-by-layer guide.** [GUIDE.md](https://github.com/cristianbarragan/Foundgine/blob/main/samples/Foundgine.SupplyChain/GUIDE.md) is the source for this page.
- **See how a request actually executes.** The [How it works](../how-it-works/index.html) page follows structured intent through authorization, planning and execution.
- **Look at the evidence.** The [Agent Benchmark](../agent-benchmark/index.html) page includes a dedicated Supply Chain end-to-end report.