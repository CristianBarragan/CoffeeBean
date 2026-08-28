using Foundgine.Aot;

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

/// <summary>Domain types are also the CLR source observed by the AOT metadata producer.</summary>
[FoundgineEntity("Company", StorageName = "companies", Id = 1000)]
public sealed record Company([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] CompanyId Id, string Name, string TenantId);

[FoundgineEntity("BusinessUnit", StorageName = "business_units", Id = 1001)]
public sealed record BusinessUnit([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] BusinessUnitId Id, CompanyId CompanyId, string Name);

[FoundgineEntity("Warehouse", StorageName = "warehouses", Id = 1002)]
public sealed record Warehouse([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] WarehouseId Id, BusinessUnitId BusinessUnitId, string Name, string TenantId)
{
    [FoundgineRelationship(typeof(BusinessUnit), "BusinessUnitId", "Id", Id = 1, Name = "businessUnit")]
    public BusinessUnit BusinessUnit { get; init; } = null!;
    [FoundgineRelationship(typeof(InventoryLot), "WarehouseId", "Id", Id = 2, Name = "inventory")]
    public IReadOnlyList<InventoryLot> Inventory { get; init; } = [];
}

[FoundgineEntity("Supplier", StorageName = "suppliers", Id = 1003)]
public sealed record Supplier([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] SupplierId Id, string Name, string Country, decimal RiskScore, string TenantId)
{
    [FoundgineRelationship(typeof(SupplierCertification), "SupplierId", "Id", Id = 3, Name = "certifications")]
    public IReadOnlyList<SupplierCertification> Certifications { get; init; } = [];
    [FoundgineRelationship(typeof(ComplianceIncident), "SupplierId", "Id", Id = 4, Name = "incidents")]
    public IReadOnlyList<ComplianceIncident> Incidents { get; init; } = [];
}

[FoundgineEntity("SupplierSite", StorageName = "supplier_sites", Id = 1004)]
public sealed record SupplierSite([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] SupplierSiteId Id, SupplierId SupplierId, string Country, string Name);

[FoundgineEntity("SupplierCertification", StorageName = "supplier_certifications", Id = 1005)]
public sealed record SupplierCertification([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] CertificationId Id, SupplierId SupplierId, string Type, DateOnly ValidFrom, DateOnly ValidTo);

[FoundgineEntity("Product", StorageName = "products", Id = 1006)]
public sealed record Product([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] ProductId Id, string Sku, string Name, string Category, decimal SafetyStock)
{
    [FoundgineRelationship(typeof(ProductComponent), "ParentProductId", "Id", Id = 5, Name = "components")]
    public IReadOnlyList<ProductComponent> Components { get; init; } = [];
    [FoundgineRelationship(typeof(PurchaseOrderLine), "ProductId", "Id", Id = 6, Name = "purchaseOrderLines")]
    public IReadOnlyList<PurchaseOrderLine> PurchaseOrderLines { get; init; } = [];
}

[FoundgineEntity("ProductComponent", StorageName = "product_components", Id = 1007)]
public sealed record ProductComponent([property: FoundgineField("ParentProductId", Id = 1, IsPrimaryKey = true)] ProductId ParentProductId, ProductId ComponentProductId, decimal QuantityPerParent)
{
    [FoundgineRelationship(typeof(Product), "ComponentProductId", "Id", Id = 7, Name = "componentProduct")]
    public Product ComponentProduct { get; init; } = null!;
}

public enum PurchaseOrderStatus { Open, PartiallyReceived, Cancelled, Closed }

[FoundgineEntity("PurchaseOrder", StorageName = "purchase_orders", Id = 1008)]
public sealed record PurchaseOrder([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] PurchaseOrderId Id, SupplierId SupplierId, WarehouseId WarehouseId, PurchaseOrderStatus Status, DateOnly OrderedOn, DateOnly ExpectedArrival)
{
    [FoundgineRelationship(typeof(PurchaseOrderLine), "PurchaseOrderId", "Id", Id = 8, Name = "lines")]
    public IReadOnlyList<PurchaseOrderLine> Lines { get; init; } = [];
    [FoundgineRelationship(typeof(Shipment), "PurchaseOrderId", "Id", Id = 9, Name = "shipments")]
    public IReadOnlyList<Shipment> Shipments { get; init; } = [];
    [FoundgineRelationship(typeof(Supplier), "SupplierId", "Id", Id = 10, Name = "supplier")]
    public Supplier Supplier { get; init; } = null!;
}

[FoundgineEntity("PurchaseOrderLine", StorageName = "purchase_order_lines", Id = 1009)]
public sealed record PurchaseOrderLine([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] PurchaseOrderLineId Id, PurchaseOrderId PurchaseOrderId, ProductId ProductId, decimal Quantity, decimal UnitPrice)
{
    [FoundgineRelationship(typeof(PurchaseOrder), "PurchaseOrderId", "Id", Id = 11, Name = "purchaseOrder")]
    public PurchaseOrder PurchaseOrder { get; init; } = null!;
}

public enum ShipmentStatus { Planned, InTransit, Delayed, PartiallyReceived, Received, Cancelled }

[FoundgineEntity("Shipment", StorageName = "shipments", Id = 1010)]
public sealed record Shipment([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] ShipmentId Id, PurchaseOrderId PurchaseOrderId, DateOnly ExpectedArrival, DateOnly? ActualArrival, ShipmentStatus Status, decimal Quantity);

[FoundgineEntity("InventoryLot", StorageName = "inventory", Id = 1011)]
public sealed record InventoryLot([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] InventoryLotId Id, WarehouseId WarehouseId, ProductId ProductId, decimal OnHand, decimal Reserved, decimal Quarantined, DateOnly ReceivedOn)
{
    [FoundgineRelationship(typeof(Warehouse), "WarehouseId", "Id", Id = 12, Name = "warehouse")]
    public Warehouse Warehouse { get; init; } = null!;
}

[FoundgineEntity("CustomerOrder", StorageName = "customer_orders", Id = 1012)]
public sealed record CustomerOrder([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] CustomerOrderId Id, BusinessUnitId BusinessUnitId, DateOnly PlacedOn, string Status)
{
    [FoundgineRelationship(typeof(CustomerOrderLine), "CustomerOrderId", "Id", Id = 13, Name = "lines")]
    public IReadOnlyList<CustomerOrderLine> Lines { get; init; } = [];
}

[FoundgineEntity("CustomerOrderLine", StorageName = "customer_order_lines", Id = 1013)]
public sealed record CustomerOrderLine([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] CustomerOrderLineId Id, CustomerOrderId CustomerOrderId, ProductId ProductId, decimal Quantity);

public sealed record InventoryMovement(int Id, InventoryLotId LotId, decimal Quantity, string Reason, DateTimeOffset OccurredAt);
public sealed record Allocation(CustomerOrderLineId OrderLineId, InventoryLotId LotId, decimal Quantity);
public sealed record ProductionOrder(int Id, ProductId ProductId, decimal Quantity, string Status);
public sealed record ProductionMaterial(int Id, int ProductionOrderId, ProductId ProductId, decimal Quantity);
public sealed record ProductionOutput(int Id, int ProductionOrderId, ProductId ProductId, decimal Quantity);

[FoundgineEntity("ComplianceIncident", StorageName = "compliance_incidents", Id = 1014)]
public sealed record ComplianceIncident([property: FoundgineField("Id", Id = 1, IsPrimaryKey = true)] int Id, SupplierId SupplierId, string Severity, DateOnly OccurredOn, string Description);
