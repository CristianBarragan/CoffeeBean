using System.Text.Json;
using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Xunit;

namespace Foundgine.Semantics.Tests;

/// <summary>
/// Locks the semantic capability contract used by application and AI adapters.
/// These tests deliberately stay provider- and transport-independent.
/// </summary>
public sealed class SemanticCapabilityContractTests
{
    [Fact]
    public void Capability_discovery_is_deterministically_ordered()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(2), "Zebra", e => e
                .Identity(new FieldId(20), "Id")
                .Field(new FieldId(22), "Name", typeof(string)))
            .Entity(new EntityId(1), "Account", e => e
                .Identity(new FieldId(10), "Id")
                .Field(new FieldId(12), "Name", typeof(string)))
            .Build();

        var capabilities = SemanticAuthorizationCapabilityDiscovery.Describe(
            model,
            new AllowAllSemanticAuthorizationPolicy());

        Assert.Equal(["Account", "Zebra"], capabilities.Entities.Select(x => x.Name));
        Assert.Equal(["Name"], capabilities.Entities[0].Fields.Select(x => x.Name));
    }

    [Fact]
    public void Conditional_authorization_is_described_without_exposing_the_predicate()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "TenantId", typeof(int)))
            .Build();

        var capabilities = SemanticAuthorizationCapabilityDiscovery.Describe(
            model,
            new ConditionalCustomerPolicy());

        var customer = Assert.Single(capabilities.Entities);
        Assert.Equal(AuthorizationAccess.Conditional, customer.Read.Access);
        Assert.Null(customer.Read.Predicate);
    }

    [Fact]
    public void Capability_document_is_machine_serializable_without_provider_types()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string)))
            .Build();

        var capabilities = SemanticAuthorizationCapabilityDiscovery.Describe(
            model,
            new AllowAllSemanticAuthorizationPolicy());

        var json = JsonSerializer.Serialize(capabilities);

        Assert.Contains("Customer", json, StringComparison.Ordinal);
        Assert.Contains("Name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT ", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" FROM ", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ConditionalCustomerPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override AuthorizationPredicate? GetPredicate(
            EntityId entityId,
            AuthorizationOperation operation) =>
            entityId == new EntityId(1) && operation == AuthorizationOperation.Read
                ? AuthorizationPredicate.Equal(
                    AuthorizationPredicate.Member(
                        AuthorizationPredicate.Parameter("customer"), "TenantId"),
                    AuthorizationPredicate.ContextParameter("tenantId"))
                : null;
    }
}
