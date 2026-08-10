using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.E2E.Tests;

public sealed class FoundginePipelineTests
{
    [Fact]
    public void Banking_thesis_reaches_provider_independent_plan()
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

        var graph = new SemanticGraph();
        var root = graph.AddRoot(customer);
        var accounts = graph.Add(account, new RelationshipId(1), root);
        graph.Add(transaction, new RelationshipId(2), accounts);

        var plan = new Planner().Plan(graph);

        Assert.Equal("Customer", model.Get(plan.Graph.Nodes[0].EntityId).Name);
        Assert.Equal("Account", model.Get(plan.Graph.Nodes[1].EntityId).Name);
        Assert.Equal("Transaction", model.Get(plan.Graph.Nodes[2].EntityId).Name);
    }
}
