using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.Query;
using Foundgine.GraphQL.HotChocolate;
using Xunit;

namespace Foundgine.GraphQL.HotChocolate.Tests;

public sealed class M15AggregateFilterTests
{
    private static readonly EntityId Customer = new(401);
    private static readonly EntityId Account = new(402);
    private static readonly RelationshipId CustomerAccounts = new(401);

    [Fact]
    public void Count_filter_is_translated_to_semantic_aggregate_filter()
    {
        var model = BuildModel();
        var request = new HotChocolateSemanticAdapter(model).Adapt("""
            query {
              customer(where: { accounts: { count: { gte: 2 } } }) {
                id
              }
            }
            """);

        var filter = Assert.IsType<SemanticAggregateFilter>(request.Options!.Filter);
        Assert.Equal(CustomerAccounts, filter.Relationship);
        Assert.Equal(SemanticFilterAggregate.Count, filter.Aggregate);
        Assert.Null(filter.Field);
        Assert.Equal(SemanticAggregateFilterOperator.Gte, filter.Operator);
        Assert.Equal(2L, filter.Value);
    }

    [Fact]
    public void Max_filter_resolves_target_field()
    {
        var model = BuildModel();
        var request = new HotChocolateSemanticAdapter(model).Adapt("""
            query {
              customer(where: { accounts: { balance: { max: { gt: 100 } } } }) {
                id
              }
            }
            """);

        var filter = Assert.IsType<SemanticAggregateFilter>(request.Options!.Filter);
        Assert.Equal(CustomerAccounts, filter.Relationship);
        Assert.Equal(SemanticFilterAggregate.Max, filter.Aggregate);
        Assert.Equal(new FieldId(3), filter.Field);
        Assert.Equal(SemanticAggregateFilterOperator.Gt, filter.Operator);
        Assert.Equal(100L, filter.Value);
    }

    private static SemanticModel BuildModel() =>
        new SemanticModelBuilder()
            .Entity(Customer, "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Relationship(CustomerAccounts, "Accounts", Account, RelationshipCardinality.Many))
            .Entity(Account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal)))
            .Build();
}
