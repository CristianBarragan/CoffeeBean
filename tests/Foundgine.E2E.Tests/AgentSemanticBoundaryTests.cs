using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Intent;
using Foundgine.Semantics.Query;
using Foundgine.Semantics.Resolution;
using Xunit;

namespace Foundgine.E2E.Tests;

public sealed class AgentSemanticBoundaryTests
{
    [Fact]
    public void Agent_friendly_intent_contains_meaning_not_provider_instructions()
    {
        var customer = new EntityId(1);
        var transaction = new EntityId(2);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(new RelationshipId(1), "Transactions", transaction, RelationshipCardinality.Many))
            .Entity(transaction, "Transaction", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Amount", typeof(decimal)))
            .Build();

        var intent = new ReadIntent(
            RootEntity: "Customer",
            Selections: [
                new ReadSelection(Field: "Id"),
                new ReadSelection(Field: "Name"),
                new ReadSelection(Relationship: "Transactions", Children: [
                    new ReadSelection(Field: "Id"),
                    new ReadSelection(Field: "Amount")
                ])
            ],
            Filter: new ReadFieldFilter("Name", SemanticFilterOperator.Eq, "Alice"),
            Limit: 5);

        Assert.Equal("Customer", intent.RootEntity);
        Assert.Equal("Name", intent.Selections[1].Field);
        Assert.Equal("Transactions", intent.Selections[2].Relationship);

        var request = new ReadIntentCompiler(model).Compile(intent);
        var authorized = new SemanticAuthorizer(new AllowAllSemanticAuthorizationPolicy()).Authorize(
            new SemanticRequestResolver(model).Resolve(request));

        Assert.Equal(customer, authorized.Nodes.Single(node => node.ParentId is null).EntityId);
        Assert.Contains(new FieldId(2), authorized.Nodes[0].Fields);
        var intentText = intent.ToString();
        Assert.DoesNotContain("SELECT ", intentText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" JOIN ", intentText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" FROM ", intentText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agent_intent_cannot_select_a_provider_or_bypass_authorization()
    {
        var account = new EntityId(1);
        var model = new SemanticModelBuilder()
            .Entity(account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Balance", typeof(decimal)))
            .Build();

        var intent = new ReadIntent(
            "Account",
            [new ReadSelection(Field: "Id"), new ReadSelection(Field: "Balance")]);

        var request = new ReadIntentCompiler(model).Compile(intent);
        var resolved = new SemanticRequestResolver(model).Resolve(request);

        var policy = new DenyBalancePolicy(account);
        var authorized = new SemanticAuthorizer(policy).Authorize(resolved);

        Assert.DoesNotContain(new FieldId(2), authorized.Nodes[0].Fields);
    }

    private sealed class DenyBalancePolicy(EntityId account) : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanAccessField(EntityId entityId, FieldId fieldId) =>
            entityId != account || fieldId != new FieldId(2);
    }
}
