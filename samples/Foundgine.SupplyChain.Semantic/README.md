# Foundgine Supply Chain — Complex Semantic Showcase

This is the intentionally difficult reference vertical for Foundgine. It is designed to exercise the semantic boundary rather than demonstrate CRUD.

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

## Capability definitions (Step 5/6 API)

`Semantics/SupplyChainCapabilities.cs` declares this sample's two scenario operations (`read_supplier_risk`, `write_purchasing`) as `SemanticCapabilityDefinition`s with declarative authorization requirements — tenant, warehouse-resource, and a policy requirement per operation — mirroring what the existing `AuthorizationContext` (`TenantId`, `AllowedWarehouses`, `CanReadSupplierRisk`, `CanWritePurchasing`) already establishes at runtime. `Api/Program.cs` prints these alongside the scenario output.

## Run

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic
```

The current sample contains a deterministic in-memory scenario evaluator so the domain behavior can be exercised without external infrastructure. The semantic model is intentionally shaped like the generated artifact; the next generator pass can replace that hand-emitted file with the project's AOT semantic generator.
