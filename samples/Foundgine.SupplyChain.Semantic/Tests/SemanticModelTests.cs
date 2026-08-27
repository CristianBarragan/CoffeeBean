using Foundgine.SupplyChain.Semantic.Semantics;
using Xunit;

namespace Foundgine.SupplyChain.Semantic.Tests;

public sealed class SemanticModelTests
{
    [Fact]
    public void Model_contains_the_supply_chain_entities_and_key_relationships()
    {
        var model = SupplyChainSemanticModel.Build();

        Assert.Equal(13, model.Entities.Count);
        var product = model.Get(SupplyChainSemanticModel.Product);
        Assert.Equal("Product", product.Name);
        Assert.Contains(product.Relationships, r => r.Name == "components");

        var warehouse = model.Get(SupplyChainSemanticModel.Warehouse);
        Assert.Contains(warehouse.Relationships, r => r.Name == "inventory");
        Assert.Contains(warehouse.Relationships, r => r.Name == "businessUnit");

        var purchaseOrder = model.Get(SupplyChainSemanticModel.PurchaseOrder);
        Assert.Contains(purchaseOrder.Relationships, r => r.Name == "lines");
        Assert.Contains(purchaseOrder.Relationships, r => r.Name == "shipments");
    }

    [Fact]
    public void Generated_metadata_exposes_resource_limits_for_recursive_queries_and_pagination()
    {
        Assert.Equal(5, Generated.SupplyChainGeneratedMetadata.RecursiveBomMaxDepth);
        Assert.Equal(50, Generated.SupplyChainGeneratedMetadata.MaximumPageSize);
        Assert.Equal(10000, Generated.SupplyChainGeneratedMetadata.MaximumTraversalNodes);
    }
}
