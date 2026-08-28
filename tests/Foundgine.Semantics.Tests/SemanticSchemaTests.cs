using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.Capabilities;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticSchemaTests
{
    [Fact]
    public void Schema_assigns_its_namespace_to_capabilities()
    {
        var entity = new EntityId(1);
        var model = Model(entity, "Order");
        var capability = new SemanticCapability(
            "order.read",
            "Read Order",
            entity,
            new AuthorizationDecision(AuthorizationAccess.Allowed),
            [], [], [], [], []);

        var schema = new SemanticSchema("commerce", model, [capability]);

        Assert.Equal("commerce", schema.Name);
        Assert.Equal("commerce", schema.Capabilities.Single().Schema);
    }

    [Fact]
    public void Registry_composes_generated_and_manual_schemas()
    {
        var order = new EntityId(1);
        var shipment = new EntityId(2);

        var generated = new SemanticSchema(
            "commerce",
            Model(order, "Order"));
        var manual = new SemanticSchema(
            "shipping",
            Model(shipment, "Shipment"));

        var set = new SemanticSchemaRegistry()
            .Register(generated)
            .Register(manual)
            .Build();

        Assert.Equal(2, set.Schemas.Count);
        Assert.Equal(2, set.Model.Entities.Count);
        Assert.Equal("Order", set.Model.Get(order).Name);
        Assert.Equal("Shipment", set.Model.Get(shipment).Name);
    }

    [Fact]
    public void Registry_rejects_conflicting_entity_definitions()
    {
        var entity = new EntityId(1);
        var first = new SemanticSchema("one", Model(entity, "Order"));
        var second = new SemanticSchema("two", Model(entity, "Invoice"));

        var registry = new SemanticSchemaRegistry()
            .Register(first)
            .Register(second);

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Build());
        Assert.Contains("defined differently", exception.Message, StringComparison.Ordinal);
    }

    private static SemanticModel Model(EntityId id, string name) =>
        new SemanticModelBuilder()
            .Entity(id, name, e => e.Identity(new FieldId(1), "Id"))
            .Build();
}

// Step 4 composition tests live in a separate fixture so generated/manual
// composition remains independently verifiable as the schema API evolves.



