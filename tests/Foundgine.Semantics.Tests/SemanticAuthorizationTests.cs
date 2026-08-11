using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticAuthorizationTests
{
    [Fact]
    public void Denied_field_is_removed_from_authorized_graph()
    {
        var (model, request, customer, account, _) = CreateBankingRequest();

        var resolved = new SemanticRequestResolver(model).Resolve(request);
        var authorized = new SemanticAuthorizer(new DenyBalancePolicy()).Authorize(resolved);

        Assert.Equal(3, authorized.Nodes.Count);
        Assert.Equal(new[] { new FieldId(1), new FieldId(2) }, authorized.Nodes[0].Fields);
        Assert.Equal(new[] { new FieldId(1) }, authorized.Nodes[1].Fields);
        Assert.DoesNotContain(new FieldId(3), authorized.Nodes[1].Fields);
        Assert.Equal(account, authorized.Nodes[1].EntityId);
    }

    [Fact]
    public void Denied_relationship_removes_relationship_subtree()
    {
        var (model, request, customer, _, transaction) = CreateBankingRequest();

        var resolved = new SemanticRequestResolver(model).Resolve(request);
        var authorized = new SemanticAuthorizer(new DenyTransactionsPolicy()).Authorize(resolved);

        Assert.Equal(2, authorized.Nodes.Count);
        Assert.Equal(customer, authorized.Nodes[0].EntityId);
        Assert.Equal(new EntityId(2), authorized.Nodes[1].EntityId);
        Assert.DoesNotContain(authorized.Nodes, node => node.EntityId == transaction);
    }

    [Fact]
    public void Denied_child_entity_removes_that_subtree()
    {
        var (model, request, customer, account, _) = CreateBankingRequest();

        var resolved = new SemanticRequestResolver(model).Resolve(request);
        var authorized = new SemanticAuthorizer(new DenyAccountPolicy()).Authorize(resolved);

        Assert.Single(authorized.Nodes);
        Assert.Equal(customer, authorized.Nodes[0].EntityId);
        Assert.DoesNotContain(authorized.Nodes, node => node.EntityId == account);
    }

    [Fact]
    public void Denied_root_entity_rejects_request()
    {
        var (model, request, _, _, _) = CreateBankingRequest();

        var resolved = new SemanticRequestResolver(model).Resolve(request);

        var ex = Assert.Throws<SemanticAuthorizationException>(
            () => new SemanticAuthorizer(new DenyCustomerPolicy()).Authorize(resolved));

        Assert.Contains("Access denied", ex.Message);
    }

    private static (SemanticModel Model, SemanticRequest Request, EntityId Customer, EntityId Account, EntityId Transaction)
        CreateBankingRequest()
    {
        var customer = new EntityId(1);
        var account = new EntityId(2);
        var transaction = new EntityId(3);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(new RelationshipId(1), "Accounts", account, RelationshipCardinality.Many))
            .Entity(account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Relationship(new RelationshipId(2), "Transactions", transaction, RelationshipCardinality.Many))
            .Entity(transaction, "Transaction", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

        var request = new SemanticRequest(
            customer,
            [
                new SemanticSelection(new FieldId(1), null, []),
                new SemanticSelection(new FieldId(2), null, []),
                new SemanticSelection(
                    null,
                    new RelationshipId(1),
                    [
                        new SemanticSelection(new FieldId(1), null, []),
                        new SemanticSelection(new FieldId(3), null, []),
                        new SemanticSelection(
                            null,
                            new RelationshipId(2),
                            [new SemanticSelection(new FieldId(1), null, [])])
                    ])
            ]);

        return (model, request, customer, account, transaction);
    }

    private sealed class DenyBalancePolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanAccessField(EntityId entityId, FieldId fieldId) =>
            entityId != new EntityId(2) || fieldId != new FieldId(3);
    }

    private sealed class DenyTransactionsPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId) =>
            sourceEntityId != new EntityId(2) || relationshipId != new RelationshipId(2);
    }

    private sealed class DenyAccountPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanAccessEntity(EntityId entityId) => entityId != new EntityId(2);
    }

    private sealed class DenyCustomerPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanAccessEntity(EntityId entityId) => entityId != new EntityId(1);
    }
}
