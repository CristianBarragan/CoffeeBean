using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Mapping;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticMappingTests
{
    [Fact]
    public void MappingSet_ExposesSchemasAndFlattenedCapabilities()
    {
        var capability = new SemanticCapabilityMapping(
            "order.advance_fulfillment",
            "commerce",
            new EntityId(7),
            "MyApp.CommerceMapping",
            "AdvanceFulfillment",
            "advance_fulfillment");

        var set = new SemanticMappingSet(
        [
            new SemanticSchemaMapping(
                "commerce",
                [new EntityId(7)],
                [capability])
        ]);

        Assert.Single(set.Schemas);
        Assert.Single(set.Capabilities);
        Assert.Equal("commerce", set.Capabilities[0].Schema);
        Assert.Equal(new EntityId(7), set.Capabilities[0].TargetEntityId);
    }

    [Fact]
    public void CapabilityMapping_IsConsumerNeutral()
    {
        var mapping = new SemanticCapabilityMapping(
            "order.advance_fulfillment",
            "commerce",
            new EntityId(7),
            "MyApp.CommerceMapping",
            "AdvanceFulfillment",
            "advance_fulfillment");

        Assert.DoesNotContain("Agent", mapping.ImplementationType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mcp", mapping.ImplementationType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GraphQL", mapping.ImplementationType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mapping_materializes_authorization_requirements()
    {
        var entity = new EntityId(1);

        var mapping = new SemanticCapabilityMapping(
            "place-order",
            "supply-chain",
            entity,
            "Orders.OrderService",
            "PlaceOrder",
            "execute");

        var definition = mapping.ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.place"),
                new SemanticCapabilityTenantRequirement("customer"),
                new SemanticCapabilityResourceRequirement("Order"),
                new SemanticCapabilityStateRequirement("Pending")
            ]);

        Assert.Equal(4, definition.AuthorizationRequirements.Count);

        var policy = Assert.IsType<SemanticCapabilityPolicyRequirement>(
            definition.AuthorizationRequirements[0]);
        Assert.Equal("orders.place", policy.Policy);

        var tenant = Assert.IsType<SemanticCapabilityTenantRequirement>(
            definition.AuthorizationRequirements[1]);
        Assert.Equal("customer", tenant.TenantKey);

        var resource = Assert.IsType<SemanticCapabilityResourceRequirement>(
            definition.AuthorizationRequirements[2]);
        Assert.Equal("Order", resource.ResourceType);

        var state = Assert.IsType<SemanticCapabilityStateRequirement>(
            definition.AuthorizationRequirements[3]);
        Assert.Equal("Pending", state.State);
    }

    [Fact]
    public void Mapping_without_authorization_requirements_materializes_empty_requirements()
    {
        var mapping = new SemanticCapabilityMapping(
            "read-order",
            "supply-chain",
            new EntityId(1),
            "Orders.OrderService",
            "ReadOrder",
            "read");

        var definition = mapping.ToDefinition(AuthorizationDecision.Allowed);

        Assert.Empty(definition.AuthorizationRequirements);
    }
}
