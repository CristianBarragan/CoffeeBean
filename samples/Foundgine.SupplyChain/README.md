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
Domain + Foundgine configuration
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
| `Application/SupplyChainSemanticConfiguration.cs` | application-specific semantic enrichment; structural metadata is discovered by Foundgine |
| `Infrastructure` | PostgreSQL integration and Foundgine.Sql query/mutation adapters |
| `Tests` | application, semantic and infrastructure tests |

## Foundgine source projects used

This sample is source-integrated. It uses `ProjectReference` entries into the repository `src/` tree rather than consuming an older Foundgine NuGet release. That keeps the sample aligned with the implementation being built and tested by `Foundgine.sln`.

The main Foundgine projects used are:

- `Foundgine.Abstractions`
- `Foundgine.Aot`
- `Foundgine.Aot.Generator`
- `Foundgine.Metadata`
- `Foundgine.Semantics`
- `Foundgine.Planning`
- `Foundgine.Execution`
- `Foundgine.Sql`
- `Foundgine.MCP`

External dependencies remain `ModelContextProtocol.AspNetCore` and `Npgsql`.

## Model / ERP separation

The application models and persistence entities are separate CLR contracts. The physical entities use the `ERP` suffix:

```text
Customer             → CustomerERP
SalesOrder           → SalesOrderERP
SalesOrderLine       → SalesOrderLineERP
CatalogProduct       → CatalogProductERP
InventoryPosition    → InventoryPositionERP
```

The model does not inherit from or reuse the ERP entity. Relationships and storage mappings are expressed through Foundgine metadata, while application code uses the generated semantic handles.

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

The application-facing query and mutation code does not construct numeric `FieldId` values. The AOT source generator maps the model/entity declarations and emits `Foundgine.Generated.GeneratedSemanticModel` with named field handles:

```csharp
GeneratedSemanticModel.InventoryPosition.WarehouseId.Eq(warehouseId)
GeneratedSemanticModel.InventoryPosition.ProductId.Eq(productId)
GeneratedSemanticModel.InventoryPosition.QuantityOnHand.Set(quantity)
GeneratedSemanticModel.Shipment.Status.Set("In Transit")
```

The generated handles carry the compact IDs internally, but those IDs are not part of the developer-facing contract. The model-to-ERP mapping remains explicit and lives outside both model and ERP classes.


## Using the repository source

The sample is intentionally wired to the Foundgine projects under `../../../src` instead of published Foundgine NuGet packages. This makes the sample build against the exact source tree in this repository, including the AOT source generator and current runtime changes.

For the hands-on walkthrough, see [TUTORIAL.md](TUTORIAL.md). It is the current source-tree getting-started guide and no longer contains the obsolete `0.5.x` package-install instructions.

## Lexical grounding

The sample's `Customer.Orders` relationship is also exposed with lexical aliases
such as `bought`, `purchased`, and `ordered`. These aliases are retrieval hints;
they do not change the stable relationship identity.

Foundgine's optional Elasticsearch integration can project the frozen semantic
contract into a lexical index. Domain values such as `Nike` or `Shoes` are
separate value documents and are not invented from the structural schema.
