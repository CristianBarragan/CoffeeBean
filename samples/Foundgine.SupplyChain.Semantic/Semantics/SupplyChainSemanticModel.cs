using Foundgine.Abstractions;
using Foundgine.Semantics;

namespace Foundgine.SupplyChain.Semantic.Semantics;

public static class SupplyChainSemanticModel
{
    public static readonly EntityId Product = new(1000);
    public static readonly EntityId Component = new(1001);
    public static readonly EntityId Supplier = new(1002);
    public static readonly EntityId Shipment = new(1003);
    public static readonly EntityId InventoryLot = new(1004);
    public static readonly EntityId Warehouse = new(1005);
    public static readonly EntityId BusinessUnit = new(1006);
    public static readonly EntityId CustomerOrder = new(1007);
    public static readonly EntityId CustomerOrderLine = new(1008);
    public static readonly EntityId PurchaseOrder = new(1009);
    public static readonly EntityId PurchaseOrderLine = new(1010);
    public static readonly EntityId Certification = new(1011);
    public static readonly EntityId ComplianceIncident = new(1012);

    // This is intentionally written in the same shape the semantic generator emits.
    // The domain annotations can later become the source of this generated artifact.
    public static SemanticModel Build() => new SemanticModelBuilder()
        .Entity(Product, "Product", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Sku", typeof(string))
            .Field(new FieldId(3), "Name", typeof(string))
            .Field(new FieldId(4), "Category", typeof(string))
            .Field(new FieldId(5), "SafetyStock", typeof(decimal))
            .Relationship(new RelationshipId(1), "components", Component, RelationshipCardinality.Many))
        .Entity(Component, "ProductComponent", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "ParentProductId", typeof(int))
            .Field(new FieldId(3), "ComponentProductId", typeof(int))
            .Field(new FieldId(4), "QuantityPerParent", typeof(decimal))
            .Relationship(new RelationshipId(2), "componentProduct", Product, RelationshipCardinality.One))
        .Entity(Supplier, "Supplier", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string))
            .Field(new FieldId(3), "Country", typeof(string))
            .Field(new FieldId(4), "RiskScore", typeof(decimal))
            .Relationship(new RelationshipId(3), "shipments", Shipment, RelationshipCardinality.Many)
            .Relationship(new RelationshipId(4), "certifications", Certification, RelationshipCardinality.Many)
            .Relationship(new RelationshipId(5), "incidents", ComplianceIncident, RelationshipCardinality.Many))
        .Entity(Shipment, "Shipment", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "PurchaseOrderId", typeof(int))
            .Field(new FieldId(3), "ExpectedArrival", typeof(DateOnly))
            .Field(new FieldId(4), "Status", typeof(string))
            .Field(new FieldId(5), "Quantity", typeof(decimal)))
        .Entity(InventoryLot, "InventoryLot", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "WarehouseId", typeof(int))
            .Field(new FieldId(3), "ProductId", typeof(int))
            .Field(new FieldId(4), "OnHand", typeof(decimal))
            .Field(new FieldId(5), "Reserved", typeof(decimal))
            .Field(new FieldId(6), "Quarantined", typeof(decimal))
            .Relationship(new RelationshipId(6), "warehouse", Warehouse, RelationshipCardinality.One))
        .Entity(Warehouse, "Warehouse", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "BusinessUnitId", typeof(int))
            .Field(new FieldId(3), "Name", typeof(string))
            .Field(new FieldId(4), "TenantId", typeof(string))
            .Relationship(new RelationshipId(7), "businessUnit", BusinessUnit, RelationshipCardinality.One)
            .Relationship(new RelationshipId(8), "inventory", InventoryLot, RelationshipCardinality.Many))
        .Entity(BusinessUnit, "BusinessUnit", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "CompanyId", typeof(int))
            .Field(new FieldId(3), "Name", typeof(string)))
        .Entity(CustomerOrder, "CustomerOrder", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "BusinessUnitId", typeof(int))
            .Field(new FieldId(3), "PlacedOn", typeof(DateOnly))
            .Field(new FieldId(4), "Status", typeof(string))
            .Relationship(new RelationshipId(9), "lines", CustomerOrderLine, RelationshipCardinality.Many))
        .Entity(CustomerOrderLine, "CustomerOrderLine", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "CustomerOrderId", typeof(int))
            .Field(new FieldId(3), "ProductId", typeof(int))
            .Field(new FieldId(4), "Quantity", typeof(decimal)))
        .Entity(PurchaseOrder, "PurchaseOrder", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "SupplierId", typeof(int))
            .Field(new FieldId(3), "WarehouseId", typeof(int))
            .Field(new FieldId(4), "Status", typeof(string))
            .Field(new FieldId(5), "ExpectedArrival", typeof(DateOnly))
            .Relationship(new RelationshipId(10), "lines", PurchaseOrderLine, RelationshipCardinality.Many)
            .Relationship(new RelationshipId(11), "shipments", Shipment, RelationshipCardinality.Many))
        .Entity(PurchaseOrderLine, "PurchaseOrderLine", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "PurchaseOrderId", typeof(int))
            .Field(new FieldId(3), "ProductId", typeof(int))
            .Field(new FieldId(4), "Quantity", typeof(decimal))
            .Field(new FieldId(5), "UnitPrice", typeof(decimal)))
        .Entity(Certification, "SupplierCertification", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "SupplierId", typeof(int))
            .Field(new FieldId(3), "Type", typeof(string))
            .Field(new FieldId(4), "ValidTo", typeof(DateOnly)))
        .Entity(ComplianceIncident, "ComplianceIncident", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "SupplierId", typeof(int))
            .Field(new FieldId(3), "Severity", typeof(string))
            .Field(new FieldId(4), "OccurredOn", typeof(DateOnly)))
        .Build();
}
