
# Foundgine Supply Chain — Complex Semantic Showcase

This is the intentionally difficult reference vertical for Foundgine. It is designed to exercise the semantic boundary rather than demonstrate CRUD.

> For a full walkthrough of the authorization policy and the client-claims validation
> feature described below, see **[GUIDE.md](GUIDE.md)**.

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

## Authorization showcase

The sample now treats authorization as a first-class part of the semantic example. It deliberately demonstrates all of the authorization boundaries exposed by Foundgine rather than showing only a single role check.

| Policy case | SupplyChain example | What the MCP adversary tries |
|---|---|---|
| Entity access | `ComplianceIncident` is analyst/manager only | Customer asks for incident data |
| Field access | `InventoryLot.Quarantined` is operational-only; `Supplier.RiskScore` is analyst/manager-only | Agent asks for a hidden field |
| Relationship access | `Supplier.incidents` is restricted | Agent traverses through a denied relationship |
| Conditional access | `Supplier` and `Warehouse` carry `TenantId == context.TenantId` predicates | Agent attempts a cross-tenant read |
| Write access | Inventory writes require an operational role | Read-only analyst attempts a write |
| Named operation | `inventory.reconcile` is manager-only | Operator attempts an elevated operation |
| Capability discovery | `describe_capabilities` reports allowed/denied/conditional | Agent tries to treat discovery as authority |

### Annotations versus policies

The domain models contain small declarative annotations such as `[SemanticEntity]`, `[SemanticField]`, and `[SemanticPolicy("...")]`. These annotations describe the semantic source metadata and are useful input to generation. They are **not** the runtime authorization decision.

The sample policy is configured by `SupplyChainAuthorization`. The authorization primitives themselves live in `Foundgine.Semantics`; the sample owns only domain-specific configuration:

```text
Annotations / generated metadata ─┐
Manual semantic definitions ──────┼──> SemanticModel
                                  │
Policy for current actor ─────────┘
                                      ↓
                               Authorization
                                      ↓
                             Semantic execution
```

This means an agent can discover capabilities, but the discovery response never becomes a credential. The policy is evaluated again for the actual operation.

### Client-supplied claims

`read_entity`, `write_entity`, and `policy_probe` accept an optional `claims` dictionary — extra, caller-asserted context sent alongside the (unchanged) actor/token authentication. Claims can only ever **narrow** what the authenticated role already allows; they can never widen it, and a claim that tries to assert identity directly (`role`, `tenant`, `actor`, ...) is rejected outright and fails the whole call closed.

| Claim | Effect | Direction |
|---|---|---|
| `scope=read-only` | Self-imposed write restriction for that call | Narrowing |
| `warehouse=<id>` | ANDs a warehouse-id predicate onto the existing tenant predicate | Narrowing |
| `reason`, `change_ticket` | Required evidence for the `inventory.reconcile` named operation, on top of the manager-only role check | Additional gate |
| `role`, `tenant`, `actor`, `isAdmin`, `permissions`, ... | Always rejected; the whole call fails closed | Spoofing attempt |

See **[GUIDE.md — Claims validation](GUIDE.md#claims-validation)** for the full validation rules, the policy wiring, and the complete attack/legitimate-use matrix.

## MCP adversarial client

`McpClient/` is a small protocol-level client modelled after the Run 5 benchmark. It sends untrusted `tools/call` requests and attempts to cross each authorization boundary.

Run the server:

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic/Api/Mcp/Foundgine.SupplyChain.Semantic.Mcp.Api.csproj --urls http://localhost:4782
```

Then run the client:

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic/McpClient/Foundgine.SupplyChain.Semantic.Mcp.Client.csproj
```

The client exercises:

1. capability discovery;
2. cross-tenant conditional policy probing;
3. sensitive-field access;
4. relationship escalation;
5. write escalation;
6. named-operation escalation;
7. unauthorized write;
8. authorized write as the control case;
9. client-claims identity spoofing (role and tenant injection);
10. client-claims evidence gating for the reconcile operation (missing, malformed, expired);
11. client-claims self-narrowing (read-only scope, warehouse scoping, unknown-key noise) as legitimate, honored uses.

The expected result is that an attempted privilege crossing is denied without changing the underlying semantic model or allowing the caller to supply its own authorization predicate — and that claims volunteered by a well-behaved caller to restrict itself are correctly honored rather than ignored.

### Open logical traversals

The sample also demonstrates an open-intent traversal that hides an intermediate supply-chain path. `Product.shipments` is exposed as a logical traversal while the semantic model retains `Product -> PurchaseOrderLine -> PurchaseOrder -> Shipment`. Dynamic callers can request `shipments` without knowing the intermediate entities; resolution expands the path before authorization and planning, so every hop remains enforceable.

The semantic mutation tests exercise the same open authoring model across a branching `PurchaseOrder -> PurchaseOrderLine` and `PurchaseOrder -> Shipment` dependency graph, including generated identity propagation and an update with a target filter.
