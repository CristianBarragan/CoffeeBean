using Foundgine.SupplyChain.Semantic.Authorization;

namespace Foundgine.SupplyChain.Semantic.Domain;

public readonly record struct CompanyId(int Value);
public readonly record struct BusinessUnitId(int Value);
public readonly record struct WarehouseId(int Value);
public readonly record struct SupplierId(int Value);
public readonly record struct SupplierSiteId(int Value);
public readonly record struct CertificationId(int Value);
public readonly record struct ProductId(int Value);
public readonly record struct PurchaseOrderId(int Value);
public readonly record struct PurchaseOrderLineId(int Value);
public readonly record struct ShipmentId(int Value);
public readonly record struct InventoryLotId(int Value);
public readonly record struct CustomerOrderId(int Value);
public readonly record struct CustomerOrderLineId(int Value);

public sealed record Company(CompanyId Id, string Name, string TenantId);
public sealed record BusinessUnit(BusinessUnitId Id, CompanyId CompanyId, string Name);
[SemanticEntity]
[SemanticPolicy("warehouse.read") ]
public sealed record Warehouse(WarehouseId Id, BusinessUnitId BusinessUnitId, [SemanticField] string Name, [SemanticField] string TenantId);
[SemanticEntity]
[SemanticPolicy("suppliers.read")]
public sealed record Supplier(SupplierId Id, [SemanticField] string Name, [SemanticField] string Country, [SemanticField] decimal RiskScore, [SemanticField] string TenantId);
public sealed record SupplierSite(SupplierSiteId Id, SupplierId SupplierId, string Country, string Name);
public sealed record SupplierCertification(CertificationId Id, SupplierId SupplierId, string Type, DateOnly ValidFrom, DateOnly ValidTo);

[SemanticEntity]
[SemanticPolicy("products.read")]
public sealed record Product(
    ProductId Id,
    [SemanticField] string Sku,
    [SemanticField] string Name,
    [SemanticField] string Category,
    decimal SafetyStock);
public sealed record ProductComponent(ProductId ParentProductId, ProductId ComponentProductId, decimal QuantityPerParent);

public enum PurchaseOrderStatus { Open, PartiallyReceived, Cancelled, Closed }
public sealed record PurchaseOrder(PurchaseOrderId Id, SupplierId SupplierId, WarehouseId WarehouseId, PurchaseOrderStatus Status, DateOnly OrderedOn, DateOnly ExpectedArrival);
public sealed record PurchaseOrderLine(PurchaseOrderLineId Id, PurchaseOrderId PurchaseOrderId, ProductId ProductId, decimal Quantity, decimal UnitPrice);

public enum ShipmentStatus { Planned, InTransit, Delayed, PartiallyReceived, Received, Cancelled }
public sealed record Shipment(ShipmentId Id, PurchaseOrderId PurchaseOrderId, DateOnly ExpectedArrival, DateOnly? ActualArrival, ShipmentStatus Status, decimal Quantity);
[SemanticEntity]
[SemanticPolicy("inventory.read")]
public sealed record InventoryLot(InventoryLotId Id, WarehouseId WarehouseId, ProductId ProductId, [SemanticField] decimal OnHand, [SemanticField] decimal Reserved, [SemanticField] decimal Quarantined, DateOnly ReceivedOn);
public sealed record InventoryMovement(int Id, InventoryLotId LotId, decimal Quantity, string Reason, DateTimeOffset OccurredAt);

public sealed record CustomerOrder(CustomerOrderId Id, BusinessUnitId BusinessUnitId, DateOnly PlacedOn, string Status);
public sealed record CustomerOrderLine(CustomerOrderLineId Id, CustomerOrderId CustomerOrderId, ProductId ProductId, decimal Quantity);
public sealed record Allocation(CustomerOrderLineId OrderLineId, InventoryLotId LotId, decimal Quantity);

public sealed record ProductionOrder(int Id, ProductId ProductId, decimal Quantity, string Status);
public sealed record ProductionMaterial(int Id, int ProductionOrderId, ProductId ProductId, decimal Quantity);
public sealed record ProductionOutput(int Id, int ProductionOrderId, ProductId ProductId, decimal Quantity);
public sealed record ComplianceIncident(int Id, SupplierId SupplierId, string Severity, DateOnly OccurredOn, string Description);
