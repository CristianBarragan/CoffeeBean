using Foundgine.Abstractions;
using Foundgine.Metadata;
using Foundgine.Semantics.Mutation;
using Foundgine.Semantics.Query;

namespace Foundgine.Generated;

/// <summary>
/// Application-facing semantic field handle for the Supply Chain sample.
///
/// The sample deliberately does not expose numeric FieldId construction to
/// application code. The runtime identity is resolved from the AOT-generated
/// metadata by entity and field name.
/// </summary>
public readonly record struct SupplyChainSemanticField(
    EntityId Entity,
    FieldId RuntimeId,
    string Name)
{
    public SemanticFieldFilter Eq(object? value) =>
        new(RuntimeId, SemanticFilterOperator.Eq, value);

    public SemanticFieldFilter Neq(object? value) =>
        new(RuntimeId, SemanticFilterOperator.Neq, value);

    public SemanticFieldFilter In(params object?[] values) =>
        new(RuntimeId, SemanticFilterOperator.In, values);

    public SemanticMutationField Set(object? value) =>
        new(RuntimeId, value);

    public SemanticOrderTerm Asc() =>
        new(RuntimeId, SemanticSortDirection.Asc);

    public SemanticOrderTerm Desc() =>
        new(RuntimeId, SemanticSortDirection.Desc);
}

/// <summary>
/// Named semantic handles used by the sample application.
///
/// Field IDs are resolved from Foundgine's generated metadata. Developers only
/// work with meaningful names such as InventoryPosition.QuantityOnHand.
/// </summary>
public static class SupplyChainSemanticFields
{
    private static EntityMetadata Entity(string name) =>
        Foundgine.Generated.GeneratedMetadata.Registry.Entities
            .Single(x => x.Name == name);

    private static SupplyChainSemanticField Field(string entityName, string fieldName)
    {
        var entity = Entity(entityName);
        var field = entity.EffectiveFields.Single(x => x.Name == fieldName);
        return new SupplyChainSemanticField(entity.EntityId, field.Id, field.Name);
    }

    private static IReadOnlyList<FieldId> All(params SupplyChainSemanticField[] fields) =>
        fields.Select(x => x.RuntimeId).ToArray();

    public static class Customer
    {
        public static EntityId Entity => FieldsEntity("CustomerERP");
        public static readonly SupplyChainSemanticField Id = Field("CustomerERP", "Id");
        public static readonly SupplyChainSemanticField FirstName = Field("CustomerERP", "FirstName");
        public static readonly SupplyChainSemanticField LastName = Field("CustomerERP", "LastName");
        public static readonly SupplyChainSemanticField Email = Field("CustomerERP", "Email");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, FirstName, LastName, Email);
    }

    public static class SalesOrder
    {
        public static EntityId Entity => FieldsEntity("SalesOrderERP");
        public static readonly SupplyChainSemanticField Id = Field("SalesOrderERP", "Id");
        public static readonly SupplyChainSemanticField CustomerId = Field("SalesOrderERP", "CustomerId");
        public static readonly SupplyChainSemanticField Status = Field("SalesOrderERP", "Status");
        public static readonly SupplyChainSemanticField TotalAmount = Field("SalesOrderERP", "TotalAmount");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, CustomerId, Status, TotalAmount);
    }

    public static class SalesOrderLine
    {
        public static EntityId Entity => FieldsEntity("SalesOrderLineERP");
        public static readonly SupplyChainSemanticField Id = Field("SalesOrderLineERP", "Id");
        public static readonly SupplyChainSemanticField OrderId = Field("SalesOrderLineERP", "OrderId");
        public static readonly SupplyChainSemanticField ProductId = Field("SalesOrderLineERP", "ProductId");
        public static readonly SupplyChainSemanticField Quantity = Field("SalesOrderLineERP", "Quantity");
        public static readonly SupplyChainSemanticField UnitPrice = Field("SalesOrderLineERP", "UnitPrice");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, OrderId, ProductId, Quantity, UnitPrice);
    }

    public static class CatalogProduct
    {
        public static EntityId Entity => FieldsEntity("CatalogProductERP");
        public static readonly SupplyChainSemanticField Id = Field("CatalogProductERP", "Id");
        public static readonly SupplyChainSemanticField Name = Field("CatalogProductERP", "Name");
        public static readonly SupplyChainSemanticField Sku = Field("CatalogProductERP", "Sku");
        public static readonly SupplyChainSemanticField UnitPrice = Field("CatalogProductERP", "UnitPrice");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, Name, Sku, UnitPrice);
    }

    public static class Supplier
    {
        public static EntityId Entity => FieldsEntity("SupplierERP");
        public static readonly SupplyChainSemanticField Id = Field("SupplierERP", "Id");
        public static readonly SupplyChainSemanticField Name = Field("SupplierERP", "Name");
        public static readonly SupplyChainSemanticField Email = Field("SupplierERP", "Email");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, Name, Email);
    }

    public static class Category
    {
        public static EntityId Entity => FieldsEntity("CategoryERP");
        public static readonly SupplyChainSemanticField Id = Field("CategoryERP", "Id");
        public static readonly SupplyChainSemanticField Name = Field("CategoryERP", "Name");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, Name);
    }

    public static class InventoryPosition
    {
        public static EntityId Entity => FieldsEntity("InventoryPositionERP");
        public static readonly SupplyChainSemanticField Id = Field("InventoryPositionERP", "Id");
        public static readonly SupplyChainSemanticField WarehouseId = Field("InventoryPositionERP", "WarehouseId");
        public static readonly SupplyChainSemanticField ProductId = Field("InventoryPositionERP", "ProductId");
        public static readonly SupplyChainSemanticField QuantityOnHand = Field("InventoryPositionERP", "QuantityOnHand");
        public static readonly SupplyChainSemanticField ReorderLevel = Field("InventoryPositionERP", "ReorderLevel");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, WarehouseId, ProductId, QuantityOnHand, ReorderLevel);
    }

    public static class Warehouse
    {
        public static EntityId Entity => FieldsEntity("WarehouseERP");
        public static readonly SupplyChainSemanticField Id = Field("WarehouseERP", "Id");
        public static readonly SupplyChainSemanticField Name = Field("WarehouseERP", "Name");
        public static readonly SupplyChainSemanticField Location = Field("WarehouseERP", "Location");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, Name, Location);
    }

    public static class Shipment
    {
        public static EntityId Entity => FieldsEntity("ShipmentERP");
        public static readonly SupplyChainSemanticField Id = Field("ShipmentERP", "Id");
        public static readonly SupplyChainSemanticField OrderId = Field("ShipmentERP", "OrderId");
        public static readonly SupplyChainSemanticField CarrierId = Field("ShipmentERP", "CarrierId");
        public static readonly SupplyChainSemanticField WarehouseId = Field("ShipmentERP", "WarehouseId");
        public static readonly SupplyChainSemanticField TrackingNumber = Field("ShipmentERP", "TrackingNumber");
        public static readonly SupplyChainSemanticField Status = Field("ShipmentERP", "Status");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, OrderId, CarrierId, WarehouseId, TrackingNumber, Status);
    }

    public static class Carrier
    {
        public static EntityId Entity => FieldsEntity("CarrierERP");
        public static readonly SupplyChainSemanticField Id = Field("CarrierERP", "Id");
        public static readonly SupplyChainSemanticField Name = Field("CarrierERP", "Name");
        public static IReadOnlyList<FieldId> All { get; } = All(Id, Name);
    }

    private static EntityId FieldsEntity(string name) => Entity(name).EntityId;
}
