# Foundgine Supply Chain — Getting Started

This sample is the reference implementation for the current Foundgine source tree.
It is intentionally wired to `src/` with `ProjectReference` entries so the sample exercises the same code that is built and tested in this repository.

> **Important:** this tutorial describes the repository as it exists today. It does **not** install Foundgine NuGet packages. If you copy the sample into another repository, replace the Foundgine `ProjectReference` entries with the released package versions appropriate for that repository.

## 0. What we are building

The request path is:

```text
MCP client
   ↓
Api
   ↓
Application
   ↓
Domain + generated semantics
   ↓
Foundgine Planning / Execution
   ↓
Foundgine.Sql
   ↓
PostgreSQL
```

The sample also demonstrates the separation between application models and persistence entities:

```text
Customer              → CustomerERP
SalesOrder            → SalesOrderERP
SalesOrderLine        → SalesOrderLineERP
CatalogProduct        → CatalogProductERP
InventoryPosition     → InventoryPositionERP
...
```

The application model does not inherit from, reference, or reuse the ERP CLR type. The persistence model is free to use storage names such as `customer_id`, `order_items`, and `shipping_status` without leaking those details into the semantic vocabulary.

## 1. Repository layout

The sample is already part of the main solution:

```text
samples/Foundgine.SupplyChain/
├── Domain/
│   ├── Models.cs
│   ├── StorageModels.cs
│   └── Foundgine.SupplyChain.Domain.csproj
├── Semantics/
│   ├── SupplyChainSemanticModel.cs
│   └── Foundgine.SupplyChain.Semantics.csproj
├── Application/
│   ├── Contracts.cs
│   ├── Authorization.cs
│   ├── SupplyChainApplication.cs
│   └── Foundgine.SupplyChain.Application.csproj
├── Infrastructure/
│   ├── Queries/
│   ├── Mutations/
│   └── Foundgine.SupplyChain.Infrastructure.csproj
├── Api/
│   ├── Program.cs
│   └── Foundgine.SupplyChain.Api.csproj
└── Tests/
```

Unlike the old package-based tutorial, there is no separate `Entities` project and no separate `Model` project. `Domain` owns the two CLR representations while keeping them completely separate types.

## 2. Build from the source tree

From the repository root:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln --configuration Release
```

Run the Supply Chain tests:

```bash
dotnet test samples/Foundgine.SupplyChain/Tests/Foundgine.SupplyChain.Tests.csproj --configuration Release
```

The sample's Foundgine dependencies are source projects, not NuGet packages:

```xml
<ProjectReference Include="../../../src/Foundgine.Aot/Foundgine.Aot.csproj" />
<ProjectReference Include="../../../src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"
                  PrivateAssets="all" />
```

The other sample projects similarly reference the relevant projects under `src/`.

## 3. Define the application models

Application-facing models live in `Domain/Models.cs`.

For example:

```csharp
[FoundgineModel("Customer", Id = 101)]
public sealed class Customer
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
}
```

The important point is the name: the application model is `Customer`, not `CustomerERP`.

## 4. Define persistence entities separately

Persistence entities live in `Domain/StorageModels.cs`.

For example:

```csharp
[FoundgineEntity("CustomerERP", StorageName = "customers", Id = 1)]
public sealed class CustomerERP
{
    [FoundgineField("Id", StorageName = "customer_id", Id = 1, IsPrimaryKey = true)]
    public int Id { get; init; }

    [FoundgineField("FirstName", StorageName = "first_name", Id = 2)]
    public string FirstName { get; init; } = "";

    [FoundgineField("LastName", StorageName = "last_name", Id = 3)]
    public string LastName { get; init; } = "";

    [FoundgineField("Email", StorageName = "email", Id = 4)]
    public string Email { get; init; } = "";
}
```

The ERP type is a storage representation. It is not the application's semantic model.

Relationships are declared on the persistence side with `FoundgineRelationship`, for example:

```csharp
[FoundgineRelationship(
    typeof(SalesOrderERP),
    "CustomerId",
    "Id",
    Id = 1,
    Name = "Orders")]
public IReadOnlyList<SalesOrderERP> Orders { get; init; } = [];
```

A model-side relationship can expose the application vocabulary without coupling the model to the ERP CLR type:

```csharp
[FoundgineConnection(Id = 101, Name = "Orders")]
public object Orders => throw new NotSupportedException();
```

The target is supplied separately by `Domain/Mappings.cs`:

```csharp
[FoundgineConnectionMap(typeof(Customer), nameof(Customer.Orders), typeof(SalesOrderERP))]
```

The same file contains the model/entity mappings:

```csharp
[FoundgineModelEntityMap(typeof(Customer), typeof(CustomerERP))]
[FoundgineModelEntityMap(typeof(SalesOrder), typeof(SalesOrderERP))]
```

This is schema metadata, not object navigation. Neither the application model nor the ERP entity depends on the other CLR type.

## 5. Let the AOT generator produce runtime metadata

The `Domain` project references both `Foundgine.Aot` and the source generator:

```xml
<ItemGroup>
  <ProjectReference Include="../../../src/Foundgine.Aot/Foundgine.Aot.csproj" />
  <ProjectReference Include="../../../src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false"
                    PrivateAssets="all" />
</ItemGroup>
```

The generator reads the annotated model/entity declarations and emits the runtime metadata used by the semantic layer.

Do not manually maintain the generated metadata or construct numeric runtime identifiers in application query code.

## 6. Build the semantic surface

`Semantics/SupplyChainSemanticModel.cs` exposes the generated registry:

```csharp
public static MetadataRegistry Registry { get; } = GeneratedMetadata.Registry;
public static IMetadataProvider Metadata => Registry;
```

The sample resolves entity and relationship identities by name from that registry.

`GeneratedSemanticModel` then exposes named application handles such as:

```csharp
GeneratedSemanticModel.InventoryPosition.WarehouseId
GeneratedSemanticModel.InventoryPosition.ProductId
GeneratedSemanticModel.InventoryPosition.QuantityOnHand
```

A filter is built without a hard-coded `FieldId`:

```csharp
var filter = GeneratedSemanticModel.InventoryPosition.ProductId.Eq(productId);
```

Ordering is similarly expressed through the semantic handle:

```csharp
var order = GeneratedSemanticModel.InventoryPosition.WarehouseId.Asc();
```

A mutation value uses the same handle:

```csharp
var update = GeneratedSemanticModel.InventoryPosition.QuantityOnHand.Set(quantity);
```

## 7. Create a semantic query

The query repository constructs a provider-neutral `SemanticOperation`.

A simple read looks like:

```csharp
var operation = new SemanticOperation(
    new SemanticReadNode(
        1,
        GeneratedSemanticModel.SalesOrder.Entity,
        GeneratedSemanticModel.SalesOrder.All,
        null,
        null,
        [],
        new SemanticQueryOptions(
            GeneratedSemanticModel.SalesOrder.CustomerId.Eq(customerId),
            [GeneratedSemanticModel.SalesOrder.Id.Asc()])));
```

There is no SQL string in the normal query path.

The repository passes the operation to `SemanticSqlQueryExecutor`:

```text
SemanticOperation
      ↓
Planner
      ↓
Semantic plan
      ↓
SqlCompiler
      ↓
SqlPlan
      ↓
SqlExecutionProvider
      ↓
PostgreSQL
```

## 8. Relationship traversal

Relationships are part of the semantic graph rather than handwritten SQL joins.

For example, an order can contain order lines:

```csharp
var line = new SemanticReadNode(
    2,
    GeneratedSemanticModel.SalesOrderLine.Entity,
    GeneratedSemanticModel.SalesOrderLine.All,
    SupplyChainSemanticModel.OrderLines,
    null,
    []);
```

The parent read then contains that child node.

The same pattern is used for shipment ownership checks and other graph traversals.

## 9. Application and authorization

`Application` owns use cases and application authorization. The API does not directly manipulate the planner or SQL compiler.

The flow is:

```text
MCP tool
   ↓
SupplyChainApplication
   ↓
Capability authorization
   ↓
SupplyChainQueries / SupplyChainMutations
   ↓
semantic execution
```

This keeps transport concerns out of the domain and infrastructure layers.

## 10. Infrastructure

`Infrastructure/DependencyInjection.cs` registers the source-tree Foundgine services used by the sample:

```csharp
services.AddSingleton<IMetadataProvider>(_ => SupplyChainSemanticModel.Metadata);
services.AddSingleton(SupplyChainSemanticModel.Metadata);
services.AddSingleton<Planner>();
services.AddSingleton<SemanticSqlQueryExecutor>();
```

The only external persistence package used here is `Npgsql`.

Normal queries use Foundgine planning and SQL compilation. The high-assurance `PlaceOrder` and `CancelOrder` workflows retain explicit PostgreSQL transaction code because they require database-specific locking, idempotency and atomic inventory invariants.

That boundary is intentional: Foundgine handles the semantic planning surface; application-specific transaction orchestration remains explicit where the invariant requires it.

## 11. API and MCP

`Api/Program.cs` wires the application and infrastructure services and exposes MCP over HTTP.

Run it from the repository root:

```powershell
dotnet run --project samples/Foundgine.SupplyChain/Api/Foundgine.SupplyChain.Api.csproj
```

Set the connection string first:

```powershell
$env:SupplyChainConnectionString="Host=localhost;Port=55432;Database=foundgine_supply_chain;Username=foundgine;Password=foundgine"
```

Or use the sample's Docker Compose environment:

```powershell
cd samples/Foundgine.SupplyChain
docker compose up --build
```

The default endpoints are:

```text
MCP:    http://localhost:4422/mcp
Health: http://localhost:4422/health
Ready:  http://localhost:4422/health/ready
```

## 12. Tests

The sample tests cover the generated/AOT and planning surface without requiring a package download for Foundgine itself.

Run:

```bash
dotnet test samples/Foundgine.SupplyChain/Tests/Foundgine.SupplyChain.Tests.csproj --configuration Release
```

For the repository-wide gate:

```bash
dotnet test Foundgine.sln --configuration Release
```

## 13. What is generated vs handwritten?

### Handwritten

- Application models
- ERP/storage entities
- Model/entity metadata declarations
- Application use cases
- Authorization policy
- Query/mutation composition
- PostgreSQL-specific transaction workflows
- API/MCP transport wiring

### Generated

- AOT metadata registry
- Runtime entity/field metadata consumed by the semantic layer
- Generator-backed identity resolution used by the sample semantic surface

### Do not hand-maintain

- Generated metadata tables
- Numeric runtime IDs in application query code
- Ordinary SQL for semantic query operations

## 14. Common mistakes

### Mistake 1 — Model inherits from ERP entity

Don't do this:

```csharp
public sealed class Customer : CustomerERP { }
```

The model and persistence entity are separate contracts.

### Mistake 2 — Application code constructs numeric FieldIds

Don't do this:

```csharp
new FieldId(3)
```

Use the generated/named semantic handle:

```csharp
GeneratedSemanticModel.Customer.Email.Eq(email)
```

### Mistake 3 — Put SQL in every repository method

Build a semantic operation and let Foundgine plan and compile it.

Keep explicit SQL only where the database-specific transaction invariant genuinely requires it.

### Mistake 4 — Reintroduce NuGet references into the repository sample

The checked-in sample must reference `../../../src/...` so changes to Foundgine are exercised by the sample immediately.

## 15. Five-minute mental model

```text
Entity
  = physical persistence representation

Model
  = application-facing representation

Metadata
  = generated description of the relationship between them

Semantic API
  = named, provider-neutral operations

Planner
  = turns semantic operations into executable plans

Sql
  = compiles those plans for PostgreSQL

MCP
  = exposes the application boundary to an agent
```

The key architectural rule is that **models and entities are different contracts**. The `ERP` suffix makes the physical representation explicit and prevents accidental coupling.

## 16. If you copy the sample outside this repository

The sample is deliberately source-wired for repository development.

If you move it to another repository, replace Foundgine `ProjectReference` entries with package references to the release you want to consume. The source repository itself should continue to use project references so the sample, tests and source projects are validated together.
