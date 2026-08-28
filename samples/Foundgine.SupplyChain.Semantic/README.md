
# Foundgine Supply Chain — Complex Semantic Showcase

This is the intentionally difficult reference vertical for Foundgine. It is designed to exercise the semantic boundary rather than demonstrate CRUD.

## Manual vs generated semantic models

This sample keeps the two semantic-authoring strategies conceptually distinct.

### Manual semantic model

Manual semantics are authored against the application/domain model with strongly typed property selectors:

```csharp
new SemanticModelBuilder()
    .Entity<Product>(Product, "Product", e => e
        .Identity(x => x.Id)
        .Field(x => x.Sku)
        .Field(x => x.Name)
        .Field(x => x.SafetyStock));
```

The `x` parameter in these selectors is `Product` (the domain model type). It is **not** `SemanticEntityBuilder`, an EF `IEntityType`, or a provider metadata object. Foundgine derives the field name and CLR type from the selected property and reserves `FieldId(1)` for identity and allocates subsequent entity-local `FieldId` values.

If the semantic name intentionally differs from the domain property name, the identity can specify an explicit semantic name: `Identity(x => x.ParentProductId, "Id")`.

Relationships can also be authored with **both domain-model sides explicitly typed**. This is the preferred manual form when a relationship has a direct key correspondence:

```csharp
.Relationship<Product, ProductComponent>(
    Product, new RelationshipId(1), "components",
    product => product.Id,
    Component, component => component.ParentProductId,
    RelationshipCardinality.Many)
```

The generic pair is `<fromEntity, toModel>`. The first selector can only access `Product` properties; the second can only access `ProductComponent` properties. Foundgine verifies that the selected CLR property types match.

Not every semantic edge has a direct property-to-property correspondence. The sample's `Supplier -> Shipment` edge is therefore intentionally retained as a topology-only relationship because `Shipment` reaches the supplier indirectly through `PurchaseOrder`.

### Generated semantic model

The generated approach derives semantic handles from the application's declared model/metadata and exposes generated fields without manually constructing `FieldId` values. The generated artifact and the manual builder feed the same provider-independent semantic runtime.

The distinction is therefore **how the semantic model is authored**, not a different execution architecture:

```text
Manual domain-property selectors ──┐
                                   ├──> SemanticModel ──> Resolution ──> Planning ──> Execution
Generated semantic metadata ──────┘
```

Use manual semantics when the exposed semantic surface needs deliberate curation or differs from the complete application model. Use generated semantics when the semantic surface can safely be derived from the declared model and you want less hand-written mapping code.


## Domain

The sample models companies, business units, warehouses, suppliers, supplier certifications, products, recursive BOM components, purchase orders, shipments, inventory lots, customer orders, allocations, production and compliance incidents.

## Scenarios

1. **Deep supply-chain investigation** — multi-hop product → component → supplier → purchase order → shipment → inventory relationships.
2. **Recursive supplier risk** — bounded BOM traversal, deduplication and deliberate cycle detection.
3. **Fulfillment planning** — available inventory, reservations, quarantine, inbound shipments, temporal filtering, aggregation, stable ordering and authorization.
4. **Transactional planning** — the domain is structured for generated mutation dependency graphs: purchase order → lines → shipment → lot → inventory movement.
5. **Adversarial security** — cross-tenant warehouse, expired certification and BOM-cycle fixtures.

## Deliberately hostile data

The seed data contains a BOM cycle, delayed and partial shipments, cancelled purchase orders, quarantined inventory, cross-tenant data and an expired certification.

## Architectural intent

The important boundary is:

```text
Domain model
    ↓
Semantic metadata / generated semantics
    ↓
AI intent
    ↓
Resolution
    ↓
Authorization
    ↓
Optimization
    ↓
Execution IR
    ↓
Provider lowering
```

The agent should express intent. It should not choose tables, inject tenant predicates, bypass authorization, or construct provider-specific execution details.

## Run

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic
```

The current sample contains a deterministic in-memory scenario evaluator so the domain behavior can be exercised without external infrastructure. The semantic model is intentionally shaped like the generated artifact; the next generator pass can replace that hand-emitted file with the project's AOT semantic generator.
