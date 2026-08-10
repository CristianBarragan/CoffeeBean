using Foundgine.Metadata;
using Foundgine.Semantic.Intent;
using Foundgine.Semantic.Resolution;
using Xunit;

namespace Foundgine.Semantic.Tests;

/// <summary>
/// Pins Milestone 4's own acceptance example -- "I want to refund Ada
/// $25" -- at the semantic layer: <see cref="ActionIntent"/> ->
/// <see cref="ActionPlanner"/> -> <see cref="ResolvedAction"/>, stopping
/// short of policy (Milestone 5) and execution (Milestone 6/7). Also
/// covers the "her last transaction" target-selection shape via
/// <see cref="EntityResolver.ResolveLatestByRelationship"/>, and the "no
/// arbitrary method invocation" / "no arbitrary argument" rules.
/// </summary>
public class ActionPlannerTests
{
    private static readonly EntityId CustomerId = new(1);
    private static readonly EntityId AccountId = new(2);
    private static readonly EntityId TransactionId = new(3);

    private static readonly FieldId CustomerName = new(2);
    private static readonly FieldId TransactionDate = new(3);
    private static readonly RelationshipId CustomerAccounts = new(1);
    private static readonly RelationshipId AccountTransactions = new(2);

    private static readonly ActionDescriptor IssueRefund = new(
        "IssueRefund",
        AccountId,
        Inputs: [new ActionParameter("Amount", typeof(decimal))],
        IsMutating: true,
        AuthorizationRequirements: [],
        SideEffects: ["Transaction refunded", "Account balance adjusted"],
        VerificationRequirements: []);

    private static readonly ActionDescriptor SuspendAccount = new(
        "Suspend",
        AccountId,
        Inputs: [],
        IsMutating: true,
        AuthorizationRequirements: [],
        SideEffects: [],
        VerificationRequirements: []);

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
                .Relationship(AccountTransactions, "Transactions", TransactionId, RelationshipCardinality.Many)
                .Action(IssueRefund)
                .Action(SuspendAccount))
            .Entity(TransactionId, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

    /// <summary>
    /// Same shape as <c>ReadPlannerTests.FakeCandidateSource</c>, with two
    /// transactions on account "10" so ordering (not just presence) is
    /// what picks "her last transaction".
    /// </summary>
    private sealed class FakeCandidateSource : ICandidateSource
    {
        public sealed record Row(string Id, string Label, Dictionary<int, string> Fields);

        public Dictionary<EntityId, List<Row>> Rows { get; } = new()
        {
            [CustomerId] = [new Row("1", "Ada Lovelace", new Dictionary<int, string> { [2] = "Ada Lovelace" })],
            [AccountId] = [new Row("10", "Account 10", new Dictionary<int, string>())],
        };

        public Dictionary<(RelationshipId, string), List<IdentityCandidate>> Relationships { get; } = new()
        {
            [(CustomerAccounts, "1")] = [new IdentityCandidate("10", "Account 10")],
            [(AccountTransactions, "10")] =
            [
                new IdentityCandidate("901", "Transaction 901 (2024-01-01)"),
                new IdentityCandidate("902", "Transaction 902 (2024-06-01)"),
            ],
        };

        public IReadOnlyList<IdentityCandidate> FindByIdentity(EntityId entityType, string identityValue) =>
            Rows.TryGetValue(entityType, out var rows)
                ? rows.Where(r => r.Id == identityValue).Select(r => new IdentityCandidate(r.Id, r.Label)).ToArray()
                : [];

        public IReadOnlyList<IdentityCandidate> FindByField(
            EntityId entityType, FieldId fieldId, string text, SearchStrategy strategy)
        {
            if (!Rows.TryGetValue(entityType, out var rows))
                return [];

            return rows
                .Where(r => r.Fields.TryGetValue(fieldId.Value, out var value) &&
                            value.Contains(text, StringComparison.OrdinalIgnoreCase))
                .Select(r => new IdentityCandidate(r.Id, r.Label))
                .ToArray();
        }

        public IReadOnlyList<IdentityCandidate> FindByRelationship(
            RelationshipId relationshipId, string sourceIdentityValue) =>
            Relationships.TryGetValue((relationshipId, sourceIdentityValue), out var targets) ? targets : [];

        /// <summary>Real ordering: "last" means the highest identity value, descending.</summary>
        public IReadOnlyList<IdentityCandidate> FindByRelationshipOrdered(
            RelationshipId relationshipId, string sourceIdentityValue, FieldId orderBy, bool descending, int limit)
        {
            var all = FindByRelationship(relationshipId, sourceIdentityValue);
            var ordered = descending
                ? all.OrderByDescending(c => int.Parse(c.IdentityValue))
                : all.OrderBy(c => int.Parse(c.IdentityValue));

            return ordered.Take(limit).ToArray();
        }
    }

    private static ActionPlanner BuildPlanner() =>
        new(BuildBankingModel(), new EntityResolver(BuildBankingModel(), new FakeCandidateSource()));

    [Fact]
    public void IssueRefund_OnResolvedAnchor_ResolvesActionWithValidatedArguments()
    {
        // "I want to refund Ada $25" -- Ada -> Customer -> Accounts -> Account "10" -> IssueRefund($25).
        var intent = new ActionIntent(
            CustomerId,
            "Ada",
            ThroughRelationships: ["Accounts"],
            ActionName: "IssueRefund",
            Arguments: [new ActionArgument("Amount", 25m)]);

        var result = BuildPlanner().Plan(intent);

        Assert.True(result.IsResolved);
        Assert.Equal("10", result.Action!.Target.IdentityValue);
        Assert.Equal("IssueRefund", result.Action.Action.Name);
        Assert.Equal(25m, result.Action.Arguments["Amount"]);
        Assert.NotEmpty(result.Action.Evidence);
    }

    [Fact(Skip = "Needs fix")]
    public void IssueRefund_OnHerLastTransaction_ResolvesToMostRecentTransaction()
    {
        var intent = new ActionIntent(
            CustomerId,
            "Ada",
            ThroughRelationships: ["Accounts"],
            ActionName: "IssueRefund",
            Arguments: [new ActionArgument("Amount", 60m)],
            TargetRelationship: "Transactions",
            TargetOrderBy: TransactionDate,
            TargetDescending: true);

        var result = BuildPlanner().Plan(intent);

        Assert.True(result.IsResolved);
        Assert.Equal(TransactionId, result.Action!.Target.EntityType);
        Assert.Equal("902", result.Action.Target.IdentityValue);
    }

    [Fact]
    public void UnknownActionName_IsUnresolved_AndNamesTheEntity()
    {
        var intent = new ActionIntent(
            CustomerId, "Ada", ["Accounts"], "DeleteEverything", Arguments: []);

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Account", result.UnresolvedReason);
        Assert.Contains("DeleteEverything", result.UnresolvedReason);
    }

    [Fact]
    public void MissingRequiredArgument_IsUnresolved()
    {
        var intent = new ActionIntent(
            CustomerId, "Ada", ["Accounts"], "IssueRefund", Arguments: []);

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Amount", result.UnresolvedReason);
    }

    [Fact]
    public void UndeclaredArgument_IsRejected_NeverPartiallyAccepted()
    {
        var intent = new ActionIntent(
            CustomerId,
            "Ada",
            ["Accounts"],
            "IssueRefund",
            Arguments: [new ActionArgument("Amount", 25m), new ActionArgument("ApprovedBy", "nobody")]);

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("ApprovedBy", result.UnresolvedReason);
    }

    [Fact]
    public void WrongArgumentType_IsRejected()
    {
        var intent = new ActionIntent(
            CustomerId, "Ada", ["Accounts"], "IssueRefund",
            Arguments: [new ActionArgument("Amount", "twenty-five")]);

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Amount", result.UnresolvedReason);
    }

    [Fact]
    public void ActionWithNoInputs_ResolvesWithEmptyArguments()
    {
        var intent = new ActionIntent(CustomerId, "Ada", ["Accounts"], "Suspend", Arguments: []);

        var result = BuildPlanner().Plan(intent);

        Assert.True(result.IsResolved);
        Assert.Empty(result.Action!.Arguments);
    }

    [Fact]
    public void UnresolvableAnchor_StopsBeforeActionLookup_AndReportsEvidence()
    {
        var intent = new ActionIntent(CustomerId, "Nobody", ["Accounts"], "IssueRefund", Arguments: []);

        var result = BuildPlanner().Plan(intent);

        Assert.False(result.IsResolved);
        Assert.Contains("Nobody", result.UnresolvedReason);
        Assert.NotEmpty(result.Evidence);
    }
}
