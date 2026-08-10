using Foundgine.Metadata;
using Foundgine.Semantic.Intent;
using Foundgine.Semantic.Resolution;
using Xunit;

namespace Foundgine.Semantic.Tests;

/// <summary>
/// Pins Milestone 3's own acceptance example -- "Find Ada's last five
/// transactions" -- at the semantic layer: <see cref="ReadIntent"/> ->
/// <see cref="ReadPlanner"/> -> <see cref="ResolvedReadPlan"/>, stopping
/// short of SQL. The full pipeline down to a real database lives in
/// <c>Foundgine.Tests.ReadIntentEndToEndTests</c>, which starts from the
/// <see cref="ResolvedReadPlan"/> this produces.
/// </summary>
public class ReadPlannerTests
{
    private static readonly EntityId CustomerId = new(1);
    private static readonly EntityId AccountId = new(2);
    private static readonly EntityId TransactionId = new(3);

    private static readonly FieldId CustomerName = new(2);
    private static readonly RelationshipId CustomerAccounts = new(1);
    private static readonly RelationshipId AccountTransactions = new(2);

    private static SemanticModel BuildBankingModel() =>
        new SemanticModelBuilder()
            .Entity(CustomerId, "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(CustomerName, "Name", typeof(string))
                .Relationship(CustomerAccounts, "Accounts", AccountId, RelationshipCardinality.Many)
                .Search(new SearchCapability([CustomerName], SearchStrategy.Fuzzy)))
            .Entity(AccountId, "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Relationship(AccountTransactions, "Transactions", TransactionId, RelationshipCardinality.Many))
            .Entity(TransactionId, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

    /// <summary>Same shape as <c>ResolutionTests.FakeCandidateSource</c>, extended with an Accounts->Transactions edge.</summary>
    private sealed class FakeCandidateSource : ICandidateSource
    {
        public sealed record Row(string Id, string Label, Dictionary<int, string> Fields);

        public Dictionary<EntityId, List<Row>> Rows { get; } = new()
        {
            [CustomerId] =
            [
                new Row("1", "Ada Lovelace", new Dictionary<int, string> { [2] = "Ada Lovelace" }),
            ],
            [AccountId] = [new Row("10", "Account 10", new Dictionary<int, string>())],
        };

        public Dictionary<(RelationshipId, string), List<IdentityCandidate>> Relationships { get; } = new()
        {
            [(CustomerAccounts, "1")] = [new IdentityCandidate("10", "Account 10")],
            [(AccountTransactions, "10")] =
            [
                new IdentityCandidate("100", "100"),
                new IdentityCandidate("101", "101"),
            ],
        };

        public IReadOnlyList<IdentityCandidate> FindByIdentity(EntityId entityType, string identityValue) =>
            Rows.TryGetValue(entityType, out var rows)
                ? rows.Where(r => r.Id == identityValue)
                    .Select(r => new IdentityCandidate(r.Id, r.Label))
                    .ToArray()
                : [];

        public IReadOnlyList<IdentityCandidate> FindByField(
            EntityId entityType, FieldId fieldId, string text, SearchStrategy strategy)
        {
            if (!Rows.TryGetValue(entityType, out var rows))
                return [];

            return rows
                .Where(r => r.Fields.TryGetValue(fieldId.Value, out var value) && Matches(value, text, strategy))
                .Select(r => new IdentityCandidate(r.Id, r.Label))
                .ToArray();
        }

        public IReadOnlyList<IdentityCandidate> FindByRelationship(
            RelationshipId relationshipId, string sourceIdentityValue) =>
            Relationships.TryGetValue((relationshipId, sourceIdentityValue), out var targets) ? targets : [];

        private static bool Matches(string value, string text, SearchStrategy strategy) => strategy switch
        {
            SearchStrategy.Exact => string.Equals(value, text, StringComparison.OrdinalIgnoreCase),
            SearchStrategy.Prefix => value.StartsWith(text, StringComparison.OrdinalIgnoreCase),
            SearchStrategy.Fuzzy => value.Contains(text, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static ReadPlanner BuildPlanner() =>
        new(BuildBankingModel(), new EntityResolver(BuildBankingModel(), new FakeCandidateSource()));

    [Fact]
    public void FindAdasLastFiveTransactions_ResolvesAnchorChainAndTargetsTransactions()
    {
        var intent = new ReadIntent(
            AnchorEntity: CustomerId,
            AnchorPhrase: "Ada Lovelace",
            ThroughRelationships: ["Accounts"],
            TargetRelationship: "Transactions",
            OrderBy: new FieldId(1),
            Descending: true,
            Limit: 5);

        var result = BuildPlanner().Plan(intent);

        Assert.True(result.IsResolved);
        var plan = result.Plan!;

        // Anchor chain: Customer#1 (Ada) -> Account#10.
        Assert.Equal(2, plan.AnchorChain.Count);
        Assert.Equal(CustomerId, plan.AnchorChain[0].EntityType);
        Assert.Equal("1", plan.AnchorChain[0].IdentityValue);
        Assert.Equal(AccountId, plan.AnchorChain[1].EntityType);
        Assert.Equal("10", plan.AnchorChain[1].IdentityValue);

        // Target: bulk-query Transaction via Account.Transactions.
        Assert.Equal(TransactionId, plan.TargetEntity);
        Assert.Equal(AccountTransactions, plan.TargetRelationship);
        Assert.True(plan.Descending);
        Assert.Equal(5, plan.Limit);
        Assert.NotEmpty(plan.Evidence);
    }

    [Fact]
    public void UnknownAnchor_StopsThePlan_WithEvidence_RatherThanGuessing()
    {
        var intent = new ReadIntent(
            AnchorEntity: CustomerId,
            AnchorPhrase: "Nobody Here",
            ThroughRelationships: ["Accounts"],
            TargetRelationship: "Transactions");

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Null(result.Plan);
        Assert.Contains("Nobody Here", result.UnresolvedReason);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public void UnknownTargetRelationship_StopsThePlan_NeverInventsOne()
    {
        var intent = new ReadIntent(
            AnchorEntity: CustomerId,
            AnchorPhrase: "Ada Lovelace",
            ThroughRelationships: ["Accounts"],
            TargetRelationship: "Loans");

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Loans", result.UnresolvedReason);
    }

    [Fact]
    public void BrokenAnchorChain_StopsAtTheFailingHop()
    {
        // "Pets" isn't a relationship on Customer, so the chain should stop
        // there rather than continuing on to a TargetRelationship lookup.
        var intent = new ReadIntent(
            AnchorEntity: CustomerId,
            AnchorPhrase: "Ada Lovelace",
            ThroughRelationships: ["Pets"],
            TargetRelationship: "Transactions");

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Pets", result.UnresolvedReason);
    }
}
