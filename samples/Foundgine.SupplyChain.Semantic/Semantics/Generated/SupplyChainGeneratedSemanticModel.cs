using Foundgine.Semantics;
using Foundgine.SupplyChain.Semantic.Domain;
using Foundgine.SupplyChain.Semantic.Semantics;

namespace Foundgine.SupplyChain.Semantic.Generated;

/// <summary>
/// Representative generated semantic artifact for the sample. A real AOT
/// generation pass can replace this source file. The important property is
/// that generated entities are normal SemanticModel instances and can be
/// imported alongside manually curated entities.
/// </summary>
public static class SupplyChainGeneratedSemanticModel
{
    public static SemanticModel Build() => new SemanticModelBuilder()
        .Entity<PurchaseOrder>(SupplyChainSemanticModel.PurchaseOrder, "PurchaseOrder", e => e
            .Identity(x => x.Id)
            .Field(x => x.SupplierId)
            .Field(x => x.WarehouseId)
            .Field(x => x.Status)
            .Field(x => x.ExpectedArrival))
        .Entity<PurchaseOrderLine>(SupplyChainSemanticModel.PurchaseOrderLine, "PurchaseOrderLine", e => e
            .Identity(x => x.Id)
            .Field(x => x.PurchaseOrderId)
            .Field(x => x.ProductId)
            .Field(x => x.Quantity)
            .Field(x => x.UnitPrice))
        .Entity<Shipment>(SupplyChainSemanticModel.Shipment, "Shipment", e => e
            .Identity(x => x.Id)
            .Field(x => x.PurchaseOrderId)
            .Field(x => x.ExpectedArrival)
            .Field(x => x.ActualArrival)
            .Field(x => x.Status)
            .Field(x => x.Quantity))
        .Relationship<PurchaseOrder, PurchaseOrderLine>(
            SupplyChainSemanticModel.PurchaseOrder, new Foundgine.Abstractions.RelationshipId(10), "lines",
            order => order.Id,
            SupplyChainSemanticModel.PurchaseOrderLine, line => line.PurchaseOrderId,
            RelationshipCardinality.Many)
        .Relationship<PurchaseOrder, Shipment>(
            SupplyChainSemanticModel.PurchaseOrder, new Foundgine.Abstractions.RelationshipId(11), "shipments",
            order => order.Id,
            SupplyChainSemanticModel.Shipment, shipment => shipment.PurchaseOrderId,
            RelationshipCardinality.Many)
        .Build();
}
