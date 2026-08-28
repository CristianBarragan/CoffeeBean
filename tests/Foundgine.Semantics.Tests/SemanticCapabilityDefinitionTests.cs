using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Mapping;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticCapabilityDefinitionTests
{
    [Fact]
    public void Mapping_materializes_one_authoritative_definition()
    {
        var mapping = new SemanticCapabilityMapping(
            "order.advance_fulfillment",
            "commerce",
            new EntityId(1),
            "CommerceMapping",
            "AdvanceFulfillment",
            "advance",
            "Advance an order through fulfillment.");

        var definition = mapping.ToDefinition(AuthorizationDecision.Allowed);

        Assert.Equal("commerce.order.advance_fulfillment", definition.QualifiedName);
        Assert.Equal(new EntityId(1), definition.TargetEntityId);
        Assert.Equal("advance", definition.Capability.Operation);
        Assert.Equal("CommerceMapping", definition.Implementation!.TypeName);
        Assert.Equal("AdvanceFulfillment", definition.Implementation.MethodName);
        Assert.Equal("Advance an order through fulfillment.", definition.Metadata.Description);
    }

    [Fact]
    public void Schema_set_exposes_definitions_without_creating_consumer_specific_models()
    {
        var capability = new SemanticCapability(
            "order.read",
            "Read Order",
            new EntityId(1),
            AuthorizationDecision.Allowed,
            [], [], [], ["Id"], [])
        {
            Schema = "commerce"
        };

        var set = new SemanticSchemaRegistry()
            .Register(new SemanticSchema("commerce", Model(), [capability]))
            .Build();

        var definition = set.GetDefinition("commerce.order.read");
        Assert.Same(definition.Capability, set.GetDefinition("commerce.order.read").Capability);
        Assert.Equal("commerce", definition.Schema);
    }

    [Fact]
    public void Capability_registry_rejects_duplicate_qualified_names()
    {
        var capability = new SemanticCapability(
            "order.read", "Read Order", new EntityId(1),
            AuthorizationDecision.Allowed, [], [], [], [], [])
        { Schema = "commerce" };

        var registry = new SemanticCapabilityRegistry().Register(capability);
        var duplicate = capability with { Name = "Another Read Order" };

        Assert.Throws<InvalidOperationException>(() => registry.Register(duplicate));
    }

    private static SemanticModel Model() => new SemanticModelBuilder()
        .Entity(new EntityId(1), "Order", e => e.Identity(new FieldId(1), "Id"))
        .Build();
}
