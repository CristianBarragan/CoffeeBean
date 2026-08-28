using Foundgine.Abstractions;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Mapping;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticCapabilityAuthorizationRequirementsTests
{
    [Fact]
    public void Mapping_materializes_policy_requirement()
    {
        var mapping = CreateMapping();

        var definition = mapping.ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.read")
            ]);

        var requirement = Assert.Single(
            definition.AuthorizationRequirements);

        var policy =
            Assert.IsType<SemanticCapabilityPolicyRequirement>(requirement);

        Assert.Equal("orders.read", policy.Policy);
        Assert.Equal(SemanticCapabilityAuthorizationRequirementKind.Policy, policy.Kind);
    }

    [Fact]
    public void Mapping_materializes_tenant_requirement()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityTenantRequirement("tenant")
            ]);

        var requirement = Assert.Single(
            definition.AuthorizationRequirements);

        var tenant =
            Assert.IsType<SemanticCapabilityTenantRequirement>(requirement);

        Assert.Equal("tenant", tenant.TenantKey);
        Assert.Equal(SemanticCapabilityAuthorizationRequirementKind.Tenant, tenant.Kind);
    }

    [Fact]
    public void Mapping_materializes_resource_requirement()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityResourceRequirement("Order")
            ]);

        var requirement = Assert.Single(
            definition.AuthorizationRequirements);

        var resource =
            Assert.IsType<SemanticCapabilityResourceRequirement>(requirement);

        Assert.Equal("Order", resource.ResourceType);
        Assert.Equal(SemanticCapabilityAuthorizationRequirementKind.Resource, resource.Kind);
    }

    [Fact]
    public void Mapping_materializes_state_requirement()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityStateRequirement("Active")
            ]);

        var requirement = Assert.Single(
            definition.AuthorizationRequirements);

        var state =
            Assert.IsType<SemanticCapabilityStateRequirement>(requirement);

        Assert.Equal("Active", state.State);
        Assert.Equal(SemanticCapabilityAuthorizationRequirementKind.State, state.Kind);
    }

    [Fact]
    public void Mapping_preserves_requirement_order()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.read"),
                new SemanticCapabilityTenantRequirement("tenant"),
                new SemanticCapabilityResourceRequirement("Order"),
                new SemanticCapabilityStateRequirement("Active")
            ]);

        Assert.Collection(
            definition.AuthorizationRequirements,
            x => Assert.IsType<SemanticCapabilityPolicyRequirement>(x),
            x => Assert.IsType<SemanticCapabilityTenantRequirement>(x),
            x => Assert.IsType<SemanticCapabilityResourceRequirement>(x),
            x => Assert.IsType<SemanticCapabilityStateRequirement>(x));
    }

    [Fact]
    public void Mapping_preserves_runtime_authorization_decision()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Denied,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.read")
            ]);

        Assert.False(definition.Authorization.IsAllowed);
        Assert.Single(definition.AuthorizationRequirements);
    }

    [Fact]
    public void Mapping_preserves_implementation_binding()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.read")
            ]);

        Assert.NotNull(definition.Implementation);
        Assert.Equal("Orders", definition.Implementation!.TypeName);
        Assert.Equal("Read", definition.Implementation.MethodName);
    }

    [Fact]
    public void Mapping_preserves_metadata()
    {
        var definition = CreateMapping().ToDefinition(
            AuthorizationDecision.Allowed,
            authorizationRequirements:
            [
                new SemanticCapabilityPolicyRequirement("orders.read")
            ]);

        Assert.Equal(
            "Read an order",
            definition.Metadata.Description);
    }

    [Fact]
    public void No_requirements_produces_empty_collection()
    {
        var definition =
            CreateMapping().ToDefinition(AuthorizationDecision.Allowed);

        Assert.NotNull(definition.AuthorizationRequirements);
        Assert.Empty(definition.AuthorizationRequirements);
    }

    [Fact]
    public void Policy_requirement_rejects_empty_policy()
    {
        Assert.Throws<ArgumentException>(
            () => new SemanticCapabilityPolicyRequirement(""));
    }

    [Fact]
    public void Tenant_requirement_rejects_empty_key()
    {
        Assert.Throws<ArgumentException>(
            () => new SemanticCapabilityTenantRequirement(""));
    }

    [Fact]
    public void Resource_requirement_rejects_empty_resource_type()
    {
        Assert.Throws<ArgumentException>(
            () => new SemanticCapabilityResourceRequirement(""));
    }

    [Fact]
    public void State_requirement_rejects_empty_state()
    {
        Assert.Throws<ArgumentException>(
            () => new SemanticCapabilityStateRequirement(""));
    }

    private static SemanticCapabilityMapping CreateMapping() =>
        new(
            Id: "order.read",
            Schema: "commerce",
            TargetEntityId: new EntityId(1),
            ImplementationType: "Orders",
            MethodName: "Read",
            Operation: "read",
            Description: "Read an order");
}
