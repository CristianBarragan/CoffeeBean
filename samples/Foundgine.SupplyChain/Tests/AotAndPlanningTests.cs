using Foundgine.Abstractions;
using Foundgine.Aot;
using Foundgine.Generated;
using Foundgine.Semantics;
using Foundgine.Planning;
using Foundgine.Semantics.IR;
using Foundgine.Semantics.Query;
using Foundgine.Sql;
using Foundgine.SupplyChain.Application;
using Xunit;

namespace Foundgine.SupplyChain.Tests;

public sealed class AotAndPlanningTests
{
    [Fact]
    public void Aot_metadata_keeps_model_names_separate_from_storage_names()
    {
        var customer = GeneratedMetadata.Registry.GetEntity(SupplyChainSemanticConfiguration.Customer);
        var order = GeneratedMetadata.Registry.GetEntity(SupplyChainSemanticConfiguration.SalesOrder);

        Assert.Equal("CustomerERP", customer.Name);
        Assert.Equal("customers", customer.EffectiveStorageName);
        Assert.Equal("SalesOrderERP", order.Name);
        Assert.Equal("orders", order.EffectiveStorageName);
    }

    [Fact]
    public void Generated_semantic_surface_exposes_named_fields_without_numeric_ids()
    {
        Assert.Equal("QuantityOnHand", GeneratedSemanticModel.InventoryPosition.QuantityOnHand.Name);
        Assert.Equal(SupplyChainSemanticConfiguration.InventoryPosition, GeneratedSemanticModel.InventoryPosition.Entity);
    }

    [Fact]
    public void Semantic_query_compiles_to_provider_sql_without_repository_sql()
    {
        var operation = new SemanticOperation(new SemanticReadNode(
            1,
            SupplyChainSemanticConfiguration.CatalogProduct,
            GeneratedSemanticModel.CatalogProduct.All,
            null,
            null,
            [],
            new SemanticQueryOptions(
                GeneratedSemanticModel.CatalogProduct.Id.Eq(42))));

        var plan = new Planner().Plan(operation) with
        {
            AuthorizationBinding = new SemanticPlanAuthorizationBinding("test-contract", "test-authorization")
        };
        var sql = new SqlCompiler(GeneratedMetadata.Registry).Compile(plan);

        Assert.Contains("products", sql.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", sql.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Single(sql.EffectiveParameters);
    }

    [Fact]
    public void Semantic_model_is_discovered_from_generated_metadata_and_enriched_by_application_configuration()
    {
        var model = SupplyChainSemanticConfiguration.Model;

        Assert.Contains(model.Entities, entity => entity.Id == SupplyChainSemanticConfiguration.Customer);
        Assert.Contains(model.Entities, entity => entity.Id == SupplyChainSemanticConfiguration.Shipment);
        Assert.NotNull(model.Get(SupplyChainSemanticConfiguration.Customer).Identity);
    }
}
