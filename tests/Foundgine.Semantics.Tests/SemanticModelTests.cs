using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticModelTests
{
    [Fact]
    public void Banking_domain_can_be_described_as_semantic_model()
    {
        var customer = new EntityId(1);
        var account = new EntityId(2);
        var transaction = new EntityId(3);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(
                    new RelationshipId(1),
                    "Accounts",
                    account,
                    RelationshipCardinality.Many))
            .Entity(account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Relationship(
                    new RelationshipId(2),
                    "Transactions",
                    transaction,
                    RelationshipCardinality.Many))
            .Entity(transaction, "Transaction", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

        Assert.Equal(3, model.Entities.Count);
        Assert.Equal("Customer", model.Get(customer).Name);
        Assert.Equal(account, model.Get(customer).Relationships.Single().Target);
        Assert.Equal(transaction, model.Get(account).Relationships.Single().Target);
    }


    [Fact]
    public void Typed_manual_semantics_derive_fields_from_domain_model_properties()
    {
        var product = new EntityId(100);

        var model = new SemanticModelBuilder()
            .Entity<TestProduct>(product, "Product", e => e
                .Identity(x => x.Id)
                .Field(x => x.Sku)
                .Field(x => x.Name)
                .Field(x => x.Price))
            .Build();

        var entity = model.Get(product);

        Assert.Equal(new FieldId(1), entity.Identity.FieldId);
        Assert.Equal("Id", entity.Identity.Name);
        Assert.Collection(
            entity.Fields,
            field =>
            {
                Assert.Equal(new FieldId(2), field.Id);
                Assert.Equal("Sku", field.Name);
                Assert.Equal(typeof(string), field.ClrType);
            },
            field =>
            {
                Assert.Equal(new FieldId(3), field.Id);
                Assert.Equal("Name", field.Name);
                Assert.Equal(typeof(string), field.ClrType);
            },
            field =>
            {
                Assert.Equal(new FieldId(4), field.Id);
                Assert.Equal("Price", field.Name);
                Assert.Equal(typeof(decimal), field.ClrType);
            });
    }

    [Fact]
    public void Typed_manual_selectors_use_model_properties_not_storage_entity_properties()
    {
        var product = new EntityId(102);

        var model = new SemanticModelBuilder()
            .Entity<TestProduct>(product, "Product", entity => entity
                .Identity(model => model.Id)
                .Field(model => model.Price))
            .Build();

        Assert.NotEqual(typeof(TestProductEntity), typeof(TestProduct));
        Assert.Equal(typeof(decimal), Assert.Single(model.Get(product).Fields).ClrType);
    }

    [Fact]
    public void Typed_manual_identity_can_preserve_a_semantic_name_when_domain_property_differs()
    {
        var component = new EntityId(101);

        var model = new SemanticModelBuilder()
            .Entity<TestProductComponent>(component, "ProductComponent", e => e
                .Identity(x => x.ParentProductId, "Id")
                .Field(x => x.ParentProductId)
                .Field(x => x.ComponentProductId))
            .Build();

        var entity = model.Get(component);

        Assert.Equal(new FieldId(1), entity.Identity.FieldId);
        Assert.Equal("Id", entity.Identity.Name);
        Assert.Equal("ParentProductId", entity.Fields[0].Name);
        Assert.Equal(typeof(int), entity.Fields[0].ClrType);
    }

    private sealed record TestProduct(int Id, string Sku, string Name, decimal Price);

    // Deliberately different from TestProduct to prove the manual selector is
    // rooted in the application model, not a persistence/entity metadata type.
    private sealed record TestProductEntity(int Id, string Sku, string Name, string Price);

    private sealed record TestProductComponent(int ParentProductId, int ComponentProductId);
    private sealed record TestMismatchedComponent(Guid ParentProductId);

    [Fact]
    public void Typed_relationship_selectors_bind_both_domain_model_sides()
    {
        var product = new EntityId(110);
        var component = new EntityId(111);

        var model = new SemanticModelBuilder()
            .Entity<TestProduct>(product, "Product", e => e
                .Identity(x => x.Id))
            .Entity<TestProductComponent>(component, "ProductComponent", e => e
                .Identity(x => x.ParentProductId, "Id"))
            .Relationship<TestProduct, TestProductComponent>(
                product,
                new RelationshipId(1),
                "components",
                productModel => productModel.Id,
                component,
                componentModel => componentModel.ParentProductId,
                RelationshipCardinality.Many)
            .Build();

        Assert.Equal(component, Assert.Single(model.Get(product).Relationships).Target);
    }

    [Fact]
    public void Typed_relationship_rejects_mismatched_model_property_types()
    {
        var product = new EntityId(112);
        var component = new EntityId(113);

        Assert.Throws<ArgumentException>(() =>
            new SemanticModelBuilder()
                .Entity<TestProduct>(product, "Product", e => e
                    .Identity(x => x.Id))
                .Entity<TestMismatchedComponent>(component, "MismatchedComponent", e => e
                    .Identity(x => x.ParentProductId))
                .Relationship<TestProduct, TestMismatchedComponent>(
                    product,
                    new RelationshipId(1),
                    "components",
                    productModel => productModel.Id,
                    component,
                    componentModel => componentModel.ParentProductId,
                    RelationshipCardinality.Many)
                .Build());
    }

    [Fact]
    public void Request_graph_is_provider_independent()
    {
        var graph = new SemanticGraph();
        var customer = graph.AddRoot(new EntityId(1));
        var account = graph.Add(
            new EntityId(2),
            new RelationshipId(1),
            customer);
        var transaction = graph.Add(
            new EntityId(3),
            new RelationshipId(2),
            account);

        Assert.Equal(3, graph.Nodes.Count);
        Assert.Null(customer.ParentId);
        Assert.Equal(customer.Id, account.ParentId);
        Assert.Equal(account.Id, transaction.ParentId);
    }
}
