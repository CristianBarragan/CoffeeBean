using Foundgine.Abstractions;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticSchemaCompositionTests
{
    [Fact]
    public void Compose_merges_generated_and_manual_models_into_one_authority()
    {
        var generated = new SemanticSchema(
            "commerce",
            Model(new EntityId(1), "Order"));
        var manual = new SemanticSchema(
            "shipping",
            Model(new EntityId(2), "Shipment"));

        var set = SemanticSchemaComposition.Compose(generated, [manual]);

        Assert.Equal(2, set.Model.Entities.Count);
        Assert.Equal("Order", set.GetSchema("commerce").Model.Get(new EntityId(1)).Name);
        Assert.Equal("Shipment", set.GetSchema("shipping").Model.Get(new EntityId(2)).Name);
    }

    [Fact]
    public void Compose_rejects_generated_and_manual_schema_name_collisions()
    {
        var generated = new SemanticSchema("commerce", Model(new EntityId(1), "Order"));
        var manual = new SemanticSchema("commerce", Model(new EntityId(2), "Shipment"));

        Assert.Throws<InvalidOperationException>(() =>
            SemanticSchemaComposition.Compose(generated, [manual]));
    }

    [Fact]
    public void Compose_rejects_conflicting_entity_definitions()
    {
        var generated = new SemanticSchema("commerce", Model(new EntityId(1), "Order"));
        var manual = new SemanticSchema("shipping", Model(new EntityId(1), "Shipment"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SemanticSchemaComposition.Compose(generated, [manual]));

        Assert.Contains("defined differently", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_register_range_preserves_all_schema_boundaries()
    {
        var registry = new SemanticSchemaRegistry();
        registry.RegisterRange([
            new SemanticSchema("commerce", Model(new EntityId(1), "Order")),
            new SemanticSchema("shipping", Model(new EntityId(2), "Shipment"))]);

        var set = registry.Build();

        Assert.Equal(2, set.Schemas.Count);
        Assert.True(set.TryGetSchema("shipping", out var shipping));
        Assert.Equal("Shipment", shipping.Model.Get(new EntityId(2)).Name);
    }

    private static SemanticModel Model(EntityId id, string name) =>
        new SemanticModelBuilder()
            .Entity(id, name, e => e.Identity(new FieldId(1), "Id"))
            .Build();
}
