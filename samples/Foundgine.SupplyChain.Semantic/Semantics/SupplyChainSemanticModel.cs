using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.SupplyChain.Semantic.Domain;
using Foundgine.SupplyChain.Semantic.Generated;

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

    /// <summary>
    /// Deliberately mixed semantic model. Purchase orders, lines and shipments
    /// come from the generated artifact; the remaining domain surface is
    /// manually curated. Both paths converge through SemanticModelBuilder and
    /// therefore produce one immutable SemanticModel for authorization,
    /// resolution and planning.
    /// </summary>
    public static SemanticModel Build() => new SemanticModelBuilder()
        .Import(SupplyChainGeneratedSemanticModel.Build())
        .Entity<Product>(Product, "Product", e => e
            .Identity(x => x.Id)
            .Field(x => x.Sku)
            .Field(x => x.Name)
            .Field(x => x.Category)
            .Field(x => x.SafetyStock))
        .Entity<ProductComponent>(Component, "ProductComponent", e => e
            .Identity(x => x.ParentProductId, "Id")
            .Field(x => x.ParentProductId)
            .Field(x => x.ComponentProductId)
            .Field(x => x.QuantityPerParent))
        .Entity<Supplier>(Supplier, "Supplier", e => e
            .Identity(x => x.Id)
            .Field(x => x.Name)
            .Field(x => x.Country)
            .Field(x => x.RiskScore)
            .Field(x => x.TenantId))
        .Entity<InventoryLot>(InventoryLot, "InventoryLot", e => e
            .Identity(x => x.Id)
            .Field(x => x.WarehouseId)
            .Field(x => x.ProductId)
            .Field(x => x.OnHand)
            .Field(x => x.Reserved)
            .Field(x => x.Quarantined))
        .Entity<Warehouse>(Warehouse, "Warehouse", e => e
            .Identity(x => x.Id)
            .Field(x => x.BusinessUnitId)
            .Field(x => x.Name)
            .Field(x => x.TenantId))
        .Entity<BusinessUnit>(BusinessUnit, "BusinessUnit", e => e
            .Identity(x => x.Id)
            .Field(x => x.CompanyId)
            .Field(x => x.Name))
        .Entity<CustomerOrder>(CustomerOrder, "CustomerOrder", e => e
            .Identity(x => x.Id)
            .Field(x => x.BusinessUnitId)
            .Field(x => x.PlacedOn)
            .Field(x => x.Status))
        .Entity<CustomerOrderLine>(CustomerOrderLine, "CustomerOrderLine", e => e
            .Identity(x => x.Id)
            .Field(x => x.CustomerOrderId)
            .Field(x => x.ProductId)
            .Field(x => x.Quantity))
        .Entity<SupplierCertification>(Certification, "SupplierCertification", e => e
            .Identity(x => x.Id)
            .Field(x => x.SupplierId)
            .Field(x => x.Type)
            .Field(x => x.ValidTo))
        .Entity<ComplianceIncident>(ComplianceIncident, "ComplianceIncident", e => e
            .Identity(x => x.Id)
            .Field(x => x.SupplierId)
            .Field(x => x.Severity)
            .Field(x => x.OccurredOn))
        .Relationship<Product, ProductComponent>(
            Product, new RelationshipId(1), "components",
            product => product.Id,
            Component, component => component.ParentProductId,
            RelationshipCardinality.Many)
        .Relationship<ProductComponent, Product>(
            Component, new RelationshipId(2), "componentProduct",
            component => component.ComponentProductId,
            Product, product => product.Id,
            RelationshipCardinality.One)
        .Relationship<Supplier, SupplierCertification>(
            Supplier, new RelationshipId(4), "certifications",
            supplier => supplier.Id,
            Certification, certification => certification.SupplierId,
            RelationshipCardinality.Many)
        .Relationship<Supplier, ComplianceIncident>(
            Supplier, new RelationshipId(5), "incidents",
            supplier => supplier.Id,
            ComplianceIncident, incident => incident.SupplierId,
            RelationshipCardinality.Many)
        .Relationship<InventoryLot, Warehouse>(
            InventoryLot, new RelationshipId(6), "warehouse",
            lot => lot.WarehouseId,
            Warehouse, warehouse => warehouse.Id,
            RelationshipCardinality.One)
        .Relationship<Warehouse, BusinessUnit>(
            Warehouse, new RelationshipId(7), "businessUnit",
            warehouse => warehouse.BusinessUnitId,
            BusinessUnit, businessUnit => businessUnit.Id,
            RelationshipCardinality.One)
        .Relationship<Warehouse, InventoryLot>(
            Warehouse, new RelationshipId(8), "inventory",
            warehouse => warehouse.Id,
            InventoryLot, lot => lot.WarehouseId,
            RelationshipCardinality.Many)
        .Relationship<CustomerOrder, CustomerOrderLine>(
            CustomerOrder, new RelationshipId(9), "lines",
            order => order.Id,
            CustomerOrderLine, line => line.CustomerOrderId,
            RelationshipCardinality.Many)
        .Relationship<Product, PurchaseOrderLine>(
            Product, new RelationshipId(12), "purchaseOrderLines",
            product => product.Id,
            PurchaseOrderLine, line => line.ProductId,
            RelationshipCardinality.Many)
        .Relationship<PurchaseOrderLine, PurchaseOrder>(
            PurchaseOrderLine, new RelationshipId(13), "purchaseOrder",
            line => line.PurchaseOrderId,
            PurchaseOrder, order => order.Id,
            RelationshipCardinality.One)
        .Relationship<PurchaseOrder, Supplier>(
            PurchaseOrder, new RelationshipId(14), "supplier",
            order => order.SupplierId,
            Supplier, supplier => supplier.Id,
            RelationshipCardinality.One)
        .Traversal(Product, "shipments", new RelationshipId(12), new RelationshipId(13), new RelationshipId(11))
        .Traversal(Product, "suppliers", new RelationshipId(12), new RelationshipId(13), new RelationshipId(14))
        .Build();
}
