# Foundgine Supply Chain — Build It From Scratch

This is the **start-to-finish tutorial** for building the Supply Chain example with Foundgine.

The goal is not just to show the final code. The goal is to explain **why each project exists, what goes into it, what it is allowed to reference, and what Foundgine generates for you**.

If you are new to this sample, follow the steps in order. At the end you will understand the complete path:

```text
Entity definitions
      ↓
Model definitions
      ↓
Explicit Model ↔ ERP mapping
      ↓
AOT-generated semantic contract
      ↓
Application intent
      ↓
Foundgine planning
      ↓
SQL execution
      ↓
PostgreSQL
```

---

## 0. What we are building

We will use a small supply-chain domain:

```text
Customer
  └── Orders
        ├── Order Lines
        │     └── Product
        └── Shipments
              ├── Carrier
              └── Warehouse

Product
  └── Inventory Position
          └── Warehouse
```

There are deliberately **two different representations** of the same business concepts.

### Application models

These are the names the application and semantic layer use:

```text
Customer
SalesOrder
SalesOrderLine
CatalogProduct
InventoryPosition
Warehouse
Shipment
Carrier
```

### Persistence entities

These represent the ERP/database side:

```text
CustomerERP
SalesOrderERP
SalesOrderLineERP
CatalogProductERP
InventoryPositionERP
WarehouseERP
ShipmentERP
CarrierERP
```

The model does **not** reference the ERP type, and the ERP type does **not** reference the model.

The connection between them is declared explicitly elsewhere.

That separation is one of the most important parts of this example.

---

# 1. Create the solution

Create a new solution and the projects that will make up the application.

```powershell
mkdir FoundgineSupplyChain
cd FoundgineSupplyChain

dotnet new sln -n Foundgine.SupplyChain

dotnet new classlib -n Entities -f net9.0
dotnet new classlib -n Models -f net9.0
dotnet new classlib -n Semantics -f net9.0
dotnet new classlib -n Application -f net9.0
dotnet new classlib -n Infrastructure -f net9.0
dotnet new web -n Api -f net9.0
dotnet new xunit -n Tests -f net9.0
```

Add them to the solution:

```powershell
dotnet sln add .\Entities\Entities.csproj
dotnet sln add .\Models\Models.csproj
dotnet sln add .\Semantics\Semantics.csproj
dotnet sln add .\Application\Application.csproj
dotnet sln add .\Infrastructure\Infrastructure.csproj
dotnet sln add .\Api\Api.csproj
dotnet sln add .\Tests\Tests.csproj
```

The important thing is not the exact names. The important thing is the dependency direction.

---

# 2. Create the Entity project

The Entity project represents the **persistence/ERP side** of the application.

It knows things such as:

- database table names
- database column names
- primary keys
- foreign keys
- persistence relationships
- storage-specific metadata

It does **not** know about application models.

## 2.1 Add the Foundgine AOT package

The Entity project needs the Foundgine AOT attributes and source generator. Reference the published NuGet package rather than a Foundgine source project:

```powershell
dotnet add .\Entities\Entities.csproj package Foundgine.Aot --version 0.5.2
```

This is important for a real application: your sample consumes Foundgine the same way an external application does. The `Foundgine.Aot` package contains the AOT runtime support and generator assets, so you do not add `Foundgine.Aot.Generator` as a separate project reference.

Do **not** add a reference from Entities to Models.

The dependency must remain one-way:

```text
Entities
   ↓
Foundgine.Aot
```

not:

```text
Entities → Models
```

## 2.2 Create `CustomerERP`

Create `CustomerERP.cs`:

```csharp
using Foundgine.Aot;

namespace SupplyChain.Entities;

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

Notice the difference between the semantic field name and physical database name:

```text
Id        → customer_id
FirstName → first_name
LastName  → last_name
Email     → email
```

Foundgine uses this metadata when generating the runtime contract.

## 2.3 Create `SalesOrderERP`

```csharp
[FoundgineEntity("SalesOrderERP", StorageName = "orders", Id = 2)]
public sealed class SalesOrderERP
{
    [FoundgineField("Id", StorageName = "order_id", Id = 1, IsPrimaryKey = true)]
    public int Id { get; init; }

    [FoundgineField("CustomerId", StorageName = "customer_id", Id = 2)]
    public int CustomerId { get; init; }

    [FoundgineField("Status", StorageName = "status", Id = 3)]
    public string Status { get; init; } = "";

    [FoundgineField("TotalAmount", StorageName = "total_amount", Id = 4)]
    public decimal TotalAmount { get; init; }
}
```

## 2.4 Add the remaining ERP entities

Repeat the same pattern for:

```text
SalesOrderLineERP
CatalogProductERP
SupplierERP
CategoryERP
InventoryPositionERP
WarehouseERP
ShipmentERP
CarrierERP
```

The full checked-in sample already contains these definitions in `Domain/StorageModels.cs`.

### Important rule

The ERP project describes **where the data lives**.

It does not describe how an application agent should think about that data.

---

## 2.5 Why the tutorial uses NuGet packages

All Foundgine dependencies in this tutorial are consumed as NuGet packages. The sample's own projects (`Domain`, `Semantics`, `Application`, `Infrastructure`, `Api`, and `Tests`) remain `ProjectReference`s because they are the application being built. Foundgine itself is an external dependency and is referenced through `PackageReference`.

For this tutorial, the Foundgine package version is `0.5.2`. The relevant packages are:

```powershell
dotnet add .\Domain\Foundgine.SupplyChain.Domain.csproj package Foundgine.Aot --version 0.5.2

dotnet add .\Semantics\Foundgine.SupplyChain.Semantics.csproj package Foundgine.Abstractions --version 0.5.2
dotnet add .\Semantics\Foundgine.SupplyChain.Semantics.csproj package Foundgine.Aot --version 0.5.2
dotnet add .\Semantics\Foundgine.SupplyChain.Semantics.csproj package Foundgine.Metadata --version 0.5.2
dotnet add .\Semantics\Foundgine.SupplyChain.Semantics.csproj package Foundgine.Semantics --version 0.5.2

dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Abstractions --version 0.5.2
dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Metadata --version 0.5.2
dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Planning --version 0.5.2
dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Execution --version 0.5.2
dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Sql --version 0.5.2
dotnet add .\Infrastructure\Foundgine.SupplyChain.Infrastructure.csproj package Foundgine.Semantics --version 0.5.2

dotnet add .\Api\Foundgine.SupplyChain.Api.csproj package Foundgine.MCP --version 0.5.2
dotnet add .\Api\Foundgine.SupplyChain.Api.csproj package Foundgine.Execution --version 0.5.2
```

Do **not** add `Foundgine.Aot.Generator` directly. The AOT package supplies the generator as an analyzer.

# 3. Create the Model project

The Model project represents the **application/semantic vocabulary**.

This is what the application should think in terms of.

It should not contain:

- table names
- column names
- SQL
- database-specific types
- ERP type references

## 3.1 Add only the AOT reference

```xml
<ItemGroup>
  <PackageReference Include="Foundgine.Aot" Version="0.5.2" />
</ItemGroup>
```

Do **not** reference Entities.

That means this is valid:

```text
Models → Foundgine.Aot
```

but this is intentionally forbidden:

```text
Models → Entities
```

## 3.2 Create `Customer`

```csharp
using Foundgine.Aot;

namespace SupplyChain.Models;

[FoundgineModel("Customer", Id = 101)]
public sealed class Customer
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";

    [FoundgineConnection(Id = 101, Name = "Orders")]
    public object Orders => throw new NotSupportedException();
}
```

There is intentionally no:

```csharp
CustomerERP
```

anywhere in this class.

## 3.3 Create `SalesOrder`

```csharp
[FoundgineModel("SalesOrder", Id = 102)]
public sealed class SalesOrder
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public string Status { get; init; } = "";
    public decimal TotalAmount { get; init; }
}
```

Continue with:

```text
SalesOrderLine
CatalogProduct
Supplier
Category
InventoryPosition
Warehouse
Shipment
Carrier
```

At this point the Model project knows the business vocabulary but knows nothing about PostgreSQL.

---

# 4. Map Models to Entities

Now we need a place that knows about **both sides**.

This is the mapping/schema boundary.

The key rule is:

```text
Model       Entity
  │           │
  └──────┬────┘
         │
   explicit mapping
```

Neither type has to know about the other.

## 4.1 Create the mapping declaration

The mapping project/file needs references to:

```text
Models
Entities
Foundgine.Aot
```

Then create:

```csharp
using Foundgine.Aot;
using SupplyChain.Models;
using SupplyChain.Entities;

namespace SupplyChain.Semantics;

[Foundginemodel/entity mapping(typeof(Customer), typeof(CustomerERP))]
[Foundginemodel/entity mapping(typeof(SalesOrder), typeof(SalesOrderERP))]
[Foundginemodel/entity mapping(typeof(SalesOrderLine), typeof(SalesOrderLineERP))]
[Foundginemodel/entity mapping(typeof(CatalogProduct), typeof(CatalogProductERP))]
[Foundginemodel/entity mapping(typeof(Supplier), typeof(SupplierERP))]
[Foundginemodel/entity mapping(typeof(Category), typeof(CategoryERP))]
[Foundginemodel/entity mapping(typeof(InventoryPosition), typeof(InventoryPositionERP))]
[Foundginemodel/entity mapping(typeof(Warehouse), typeof(WarehouseERP))]
[Foundginemodel/entity mapping(typeof(Shipment), typeof(ShipmentERP))]
[Foundginemodel/entity mapping(typeof(Carrier), typeof(CarrierERP))]
internal static class SupplyChainMappings
{
}
```

This is the **only place** where the two worlds are joined.

For example:

```text
Customer
   │
   │ model/entity mapping
   ▼
CustomerERP
```

and:

```text
SalesOrder
   │
   │ model/entity mapping
   ▼
SalesOrderERP
```

---

# 5. Map relationships

Relationships need the same treatment.

For example, the model says:

```csharp
[FoundgineConnection(Id = 101, Name = "Orders")]
public object Orders => throw new NotSupportedException();
```

The model does not say what the storage target is.

The mapping does:

```csharp
[FoundgineConnection(
    typeof(Customer),
    nameof(Customer.Orders),
    typeof(SalesOrderERP))]
```

Now Foundgine knows:

```text
Customer.Orders
      ↓
SalesOrderERP
      ↓
orders
```

The important part is that `Customer` itself still has no dependency on `SalesOrderERP`.

---

# 6. Create the Semantics project

The Semantics project is where the application gets its **generated semantic contract**.

Its job is to expose the generated model in a convenient, strongly typed form.

The project needs references to the Foundgine AOT/metadata/semantics runtime and to the model/mapping declarations.

For the checked-in sample, use NuGet packages for Foundgine and a project reference only for the sample's own Domain project:

```xml
<ItemGroup>
  <PackageReference Include="Foundgine.Abstractions" Version="0.5.2" />
  <PackageReference Include="Foundgine.Aot" Version="0.5.2" />
  <PackageReference Include="Foundgine.Metadata" Version="0.5.2" />
  <PackageReference Include="Foundgine.Semantics" Version="0.5.2" />
  <ProjectReference Include="../Domain/Foundgine.SupplyChain.Domain.csproj" />
</ItemGroup>
```

The important distinction is that `Foundgine.*` dependencies are NuGet packages. Only projects that belong to your application are `ProjectReference`s.

---

# 7. Let the AOT generator run

Build the solution:

```powershell
dotnet build
```

The Foundgine source generator reads the declarations and generates the semantic/runtime contract.

Conceptually:

```text
Customer
CustomerERP
model/entity mapping(Customer, CustomerERP)
        │
        ▼
Foundgine.Aot.Generator
        │
        ├── entity metadata
        ├── field metadata
        ├── relationship metadata
        ├── model/entity mapping
        └── strongly typed semantic handles
```

The generated code is an implementation detail.

You should **not manually edit generated files**.

---

# 8. Use the generated semantic API

This is where the architecture becomes useful.

Instead of writing:

```csharp
new FieldId(2)
```

or:

```csharp
new FieldId(4)
```

you use the generated semantic names.

For example:

```csharp
SupplyChainSemanticFields.InventoryPosition.WarehouseId.Eq(warehouseId)
```

```csharp
SupplyChainSemanticFields.InventoryPosition.ProductId.Eq(productId)
```

```csharp
SupplyChainSemanticFields.InventoryPosition.QuantityOnHand.Set(quantity)
```

```csharp
SupplyChainSemanticFields.Shipment.Status.Set("In Transit")
```

The developer sees:

```text
InventoryPosition
  ├── WarehouseId
  ├── ProductId
  └── QuantityOnHand
```

not:

```text
FieldId(2)
FieldId(3)
FieldId(4)
```

The numeric IDs still exist internally when Foundgine needs compact deterministic identities, but they are no longer part of the developer-facing semantic contract.

---

# 9. Create a query

Now create an application query such as:

```csharp
public Task<object> GetOrders(int customerId, CancellationToken ct)
{
    var operation = Read(
        SupplyChainSemanticFields.SalesOrder.Entity,
        SupplyChainSemanticFields.SalesOrder.All,
        SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId),
        [SupplyChainSemanticFields.SalesOrder.Id.Asc()]);

    return Execute(operation, ct);
}
```

The important thing is what this code **does not contain**.

There is no:

```sql
SELECT * FROM orders WHERE customer_id = ...
```

There is no:

```csharp
new FieldId(2)
```

There is no:

```csharp
CustomerERP
```

The application expresses intent using the semantic model.

---

# 10. What happens after the query is created?

The full pipeline is:

```text
SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId)
                         │
                         ▼
                 Semantic operation
                         │
                         ▼
                     Planner
                         │
                         ▼
                  Execution plan
                         │
                         ▼
                   SQL compiler
                         │
                         ▼
                      SqlPlan
                         │
                         ▼
                SQL execution provider
                         │
                         ▼
                    PostgreSQL
```

The SQL compiler can use the generated mapping to know that:

```text
SalesOrder
     ↓
SalesOrderERP
     ↓
orders
```

and:

```text
SalesOrder.CustomerId
     ↓
SalesOrderERP.CustomerId
     ↓
orders.customer_id
```

The application never needs to know those storage details.

---

# 11. Create a mutation

A simple mutation follows the same pattern.

For example, update inventory:

```csharp
var filter = SemanticAndFilter([
    SupplyChainSemanticFields.InventoryPosition.WarehouseId.Eq(warehouseId),
    SupplyChainSemanticFields.InventoryPosition.ProductId.Eq(productId)
]);

var operation = SemanticMutationBuilder.Update(
    SupplyChainSemanticFields.InventoryPosition.Entity,
    [SupplyChainSemanticFields.InventoryPosition.QuantityOnHand.Set(quantity)],
    filter,
    SupplyChainSemanticFields.InventoryPosition.All);
```

Then:

```text
Mutation operation
      ↓
Mutation planner
      ↓
SQL mutation compiler
      ↓
Parameterized SQL
      ↓
PostgreSQL
```

Again, there are no numeric field IDs in application code.

---

# 12. Create the Application project

The Application project contains **use cases**, not database code.

Reference:

```text
Application
   ├── Models / domain contracts
   └── Semantics
```

It should not need a PostgreSQL connection.

For example:

```csharp
public interface ISupplyChainQueries
{
    Task<object> GetOrders(int customerId, CancellationToken ct);
    Task<object> GetProduct(int productId, CancellationToken ct);
}
```

The application layer decides **what the application is allowed to do**.

It does not decide how PostgreSQL executes it.

---

# 13. Create the Infrastructure project

Infrastructure is where Foundgine is connected to the actual provider.

It references:

```text
Application
Semantics
Foundgine.Execution
Foundgine.Planning
Foundgine.Sql
Npgsql
```

A normal query repository can therefore do:

```text
Application use case
        ↓
Generated semantic API
        ↓
Foundgine planner
        ↓
Foundgine.Sql
        ↓
Npgsql
```

The repository should not recreate the semantic metadata manually.

---

# 14. Create the API project

The API project is deliberately thin.

It should contain:

- ASP.NET configuration
- dependency injection
- MCP transport
- health endpoints
- protocol adapters

It should not contain:

- SQL
- ERP mapping
- semantic field IDs
- planner logic
- database business rules

The flow is:

```text
MCP / HTTP
    ↓
API
    ↓
Application
    ↓
Infrastructure
    ↓
Foundgine
    ↓
PostgreSQL
```

---

# 15. Add tests before adding more features

Start with the generated contract.

A useful first test is:

```csharp
[Fact]
public void Generated_semantic_surface_exposes_named_fields()
{
    Assert.Equal(
        "QuantityOnHand",
        SupplyChainSemanticFields.InventoryPosition.QuantityOnHand.Name);
}
```

Then test the mapping:

```csharp
[Fact]
public void Model_and_storage_names_are_distinct()
{
    var entity = GeneratedMetadata.Registry
        .GetEntity(SupplyChainSemanticFields.InventoryPosition.Entity);

    Assert.Equal("InventoryPositionERP", entity.Name);
    Assert.Equal("inventory", entity.EffectiveStorageName);
}
```

Then test planning:

```text
semantic request
      ↓
plan
      ↓
SQL
```

Finally test the PostgreSQL execution path.

---

# 16. Run the sample

From the repository root:

```powershell
dotnet restore
```

Then:

```powershell
dotnet build
```

Run the API:

```powershell
dotnet run --project samples/Foundgine.SupplyChain/Api/Foundgine.SupplyChain.Api.csproj
```

Or run the containerized sample:

```powershell
cd samples/Foundgine.SupplyChain
docker compose up --build
```

The sample exposes:

```text
MCP:    http://localhost:4422/mcp
Health: http://localhost:4422/health
```

---

# 17. Understand the final dependency graph

This is the most important diagram to keep in mind.

```text
                         ┌─────────────────┐
                         │     Entities    │
                         │                 │
                         │ CustomerERP     │
                         │ SalesOrderERP   │
                         │ ProductERP      │
                         │ ...             │
                         └────────┬────────┘
                                  │
                                  │ mapping boundary
                                  │
                         ┌────────▼────────┐
                         │    Semantics    │
                         │                 │
                         │ Model ↔ ERP  │
                         │ generated API   │
                         └────────┬────────┘
                                  ▲
                                  │
                         ┌────────┴────────┐
                         │      Models     │
                         │                 │
                         │ Customer        │
                         │ SalesOrder      │
                         │ Product         │
                         │ ...             │
                         └─────────────────┘

                                  │
                                  ▼
                         ┌─────────────────┐
                         │   Application   │
                         │                 │
                         │ use cases       │
                         └────────┬────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │ Infrastructure │
                         │                 │
                         │ Planner / SQL   │
                         │ PostgreSQL      │
                         └─────────────────┘
```

The critical rule is:

> **Models describe what the application means. Entities describe where the data is stored. The mapping describes how the two correspond.**

---

# 18. What is generated and what is handwritten?

## You write

```text
Entity classes
Model classes
Model ↔ ERP mappings
Application use cases
Infrastructure adapters
API/MCP adapters
Tests
```

## Foundgine generates

```text
Entity metadata
Field metadata
Relationship metadata
Model/entity metadata
Named semantic fields
Filter helpers
Mutation setters
Projection/all-field definitions
Runtime metadata registry
```

## You should not manually maintain

```text
FieldId(1)
FieldId(2)
FieldId(3)
FieldId(4)
```

or duplicated field registries.

The generator should be the source of those runtime identities.

---

# 19. Common mistakes

## Mistake 1 — Model references ERP

Do not do this:

```csharp
public CustomerERP CustomerStorage { get; }
```

The model is now coupled to persistence.

Instead, keep the model independent and map it externally.

---

## Mistake 2 — ERP references Model

Do not do this either:

```csharp
public Customer Model { get; set; }
```

The ERP representation should remain persistence-focused.

---

## Mistake 3 — Match models and entities by name

Do not rely on:

```text
Customer → Customer
```

The whole point is that these names may diverge.

Use:

```csharp
[Foundginemodel/entity mapping(typeof(Customer), typeof(CustomerERP))]
```

---

## Mistake 4 — Manually create FieldIds

Do not write:

```csharp
new FieldId(4)
```

Use:

```csharp
SupplyChainSemanticFields.InventoryPosition.QuantityOnHand
```

The generated semantic surface is the maintainable developer API.

---

## Mistake 5 — Put SQL in ordinary query repositories

Prefer:

```text
semantic intent
   ↓
planner
   ↓
SQL compiler
```

rather than embedding SQL for operations that Foundgine can express.

There are deliberate exceptions for high-assurance provider-specific workflows such as the sample's transaction-heavy order placement path.

---

# 20. The five-minute mental model

If you only remember five things, remember these:

### 1. Entity

> **Where is the data stored?**

```text
CustomerERP → customers
```

### 2. Model

> **What does the application call the concept?**

```text
Customer
```

### 3. Mapping

> **How are they related?**

```text
Customer → CustomerERP
```

### 4. Semantic API

> **How does application code express intent?**

```csharp
SupplyChainSemanticFields.Customer.Email.Eq(email)
```

### 5. Foundgine execution

> **How does that intent become execution?**

```text
semantic intent
      ↓
plan
      ↓
SQL
      ↓
database
```

That is the entire architecture in one page.

---

# 21. Where to go next

Once this basic example is understood, the next useful areas to explore are:

1. relationships and graph traversal
2. authorization capabilities
3. projections
4. pagination and sorting
5. semantic mutations
6. AOT-generated tool/MCP schemas
7. high-assurance mutations
8. provider-specific execution
9. generated semantic consistency tests
10. the AgentEndToEnd benchmark

Start with the simple query path before moving to the high-assurance transaction paths.

The objective is to understand the boundary first, then add complexity one layer at a time.
