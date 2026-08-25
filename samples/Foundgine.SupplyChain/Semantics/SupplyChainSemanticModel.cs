using Foundgine.Abstractions;
using Foundgine.Metadata;
using Foundgine.Generated;

namespace Foundgine.SupplyChain.Semantics;

public static class SupplyChainSemanticModel
{
    /// <summary>
    /// Runtime metadata emitted by the Foundgine AOT generator from the
    /// Domain project. The sample resolves identities by semantic name so
    /// application code never maintains numeric FieldId/EntityId values.
    /// </summary>
    public static MetadataRegistry Registry { get; } = GeneratedMetadata.Registry;

    public static IMetadataProvider Metadata => Registry;

    // Semantic model identities are derived from the named semantic surface.
    // Application code does not maintain numeric EntityId values.
    public static EntityId Customer => SupplyChainSemanticFields.Customer.Entity;
    public static EntityId SalesOrder => SupplyChainSemanticFields.SalesOrder.Entity;
    public static EntityId SalesOrderLine => SupplyChainSemanticFields.SalesOrderLine.Entity;
    public static EntityId CatalogProduct => SupplyChainSemanticFields.CatalogProduct.Entity;
    public static EntityId Supplier => SupplyChainSemanticFields.Supplier.Entity;
    public static EntityId Category => SupplyChainSemanticFields.Category.Entity;
    public static EntityId InventoryPosition => SupplyChainSemanticFields.InventoryPosition.Entity;
    public static EntityId Warehouse => SupplyChainSemanticFields.Warehouse.Entity;
    public static EntityId Shipment => SupplyChainSemanticFields.Shipment.Entity;
    public static EntityId Carrier => SupplyChainSemanticFields.Carrier.Entity;

    private static RelationshipId Relationship(string entityName, string relationshipName) =>
        Registry.Relationships
            .Single(x => x.Name == relationshipName &&
                         Registry.GetEntity(x.Source).Name == entityName)
            .Id;

    public static readonly RelationshipId CustomerOrders = Relationship("CustomerERP", "Orders");
    public static readonly RelationshipId OrderLines = Relationship("SalesOrderERP", "Lines");
    public static readonly RelationshipId LineProduct = Relationship("SalesOrderLineERP", "Product");
    public static readonly RelationshipId ProductSupplier = Relationship("CatalogProductERP", "Supplier");
    public static readonly RelationshipId ProductCategory = Relationship("CatalogProductERP", "Category");
    public static readonly RelationshipId ProductInventory = Relationship("CatalogProductERP", "InventoryPositions");
    public static readonly RelationshipId InventoryWarehouse = Relationship("InventoryPositionERP", "Warehouse");
    public static readonly RelationshipId OrderShipments = Relationship("SalesOrderERP", "Shipments");
    public static readonly RelationshipId ShipmentCarrier = Relationship("ShipmentERP", "Carrier");
    public static readonly RelationshipId ShipmentWarehouse = Relationship("ShipmentERP", "Warehouse");
    public static readonly RelationshipId ShipmentOrder = Relationship("ShipmentERP", "Order");
}