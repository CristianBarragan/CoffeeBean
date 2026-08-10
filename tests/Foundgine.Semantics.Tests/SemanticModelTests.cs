using Foundgine.Metadata;
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
