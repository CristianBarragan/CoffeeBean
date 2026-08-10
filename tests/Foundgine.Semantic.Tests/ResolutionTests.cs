using Foundgine.Metadata;
using Foundgine.Semantic.Resolution;
using Xunit;

namespace Foundgine.Semantic.Tests;

/// <summary>
/// Pins the Milestone 2 acceptance examples from docs/00-Direction/Milestones.md:
/// "Ada Lovelace", "account 10", and "her checking account" resolved into
/// explicit domain references, plus the "never silently invent an
/// identity" rule for the not-found and ambiguous cases.
/// </summary>
public class ResolutionTests
{
    private static readonly EntityId CustomerId = new(1);
    private static readonly EntityId AccountId = new(2);
    private static readonly EntityId TransactionId = new(3);

    private static readonly FieldId CustomerName = new(2);
    private static readonly RelationshipId CustomerAccounts = new(1);

    private static SemanticModel BuildBankingModel() =>
        new SemanticModelBuilder()
            .Entity(CustomerId, "Customer", customer => customer
                .Identity(new FieldId(1), "Id")
                .Field(CustomerName, "Name", typeof(string))
                .Relationship(CustomerAccounts, "Accounts", AccountId, RelationshipCardinality.Many)
                .Search(new SearchCapability([CustomerName], SearchStrategy.Fuzzy)))
            .Entity(AccountId, "Account", account => account
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal)))
            .Entity(TransactionId, "Transaction", transaction => transaction
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

    /// <summary>
    /// A hand-rolled <see cref="ICandidateSource"/> over the exact same
    /// Customer/Account data the Banking sample seeds into SQLite --
    /// Ada Lovelace (1) with account 10 -- plus a second, deliberately
    /// same-named customer so ambiguity has something real to trigger on.
    /// </summary>
    private sealed class FakeCandidateSource : ICandidateSource
    {
        public sealed record Row(string Id, string Label, Dictionary<int, string> Fields);

        public Dictionary<EntityId, List<Row>> Rows { get; } = new()
        {
            [CustomerId] =
            [
                new Row("1", "Ada Lovelace", new Dictionary<int, string> { [2] = "Ada Lovelace" }),
                new Row("2", "Grace Hopper", new Dictionary<int, string> { [2] = "Grace Hopper" }),
            ],
            [AccountId] =
            [
                new Row("10", "Account 10", new Dictionary<int, string>()),
            ],
        };

        public Dictionary<(RelationshipId, string), List<IdentityCandidate>> Relationships { get; } = new()
        {
            [(CustomerAccounts, "1")] = [new IdentityCandidate("10", "Account 10")],
            [(CustomerAccounts, "2")] = [],
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

    private static EntityResolver BuildResolver() => new(BuildBankingModel(), new FakeCandidateSource());

    [Fact]
    public void ResolveByIdentity_AccountTen_ResolvesWithFullConfidence()
    {
        var result = BuildResolver().ResolveByIdentity(AccountId, "10");

        Assert.Equal(ResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal("10", result.Resolved!.IdentityValue);
        Assert.Equal(1.0, result.Resolved.Confidence);
        Assert.NotEmpty(result.Resolved.Evidence);
    }

    [Fact]
    public void ResolveByIdentity_UnknownAccount_IsNotFound_AndReportsEvidence()
    {
        var result = BuildResolver().ResolveByIdentity(AccountId, "999");

        Assert.Equal(ResolutionOutcome.NotFound, result.Outcome);
        Assert.Null(result.Resolved);
        Assert.Contains("999", result.UnresolvedReason);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public void ResolveBySearch_AdaLovelace_ResolvesUniquely()
    {
        var result = BuildResolver().ResolveBySearch(CustomerId, "Ada Lovelace");

        Assert.Equal(ResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal("1", result.Resolved!.IdentityValue);
        Assert.Equal(CustomerId, result.Resolved.EntityType);
        Assert.Contains("Fuzzy", result.Resolved.Reason);
    }

    [Fact]
    public void ResolveBySearch_TextMatchingNoCustomer_IsNotFound()
    {
        var result = BuildResolver().ResolveBySearch(CustomerId, "Nobody Here");

        Assert.Equal(ResolutionOutcome.NotFound, result.Outcome);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public void ResolveBySearch_EntityWithoutSearchCapability_IsNotFound_NeverInventsAMatch()
    {
        var result = BuildResolver().ResolveBySearch(AccountId, "10");

        Assert.Equal(ResolutionOutcome.NotFound, result.Outcome);
        Assert.Contains("no search capability", result.UnresolvedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveByRelationship_HerAccounts_ResolvesThroughCustomer()
    {
        var resolver = BuildResolver();
        var ada = resolver.ResolveByIdentity(CustomerId, "1").Resolved!;

        var result = resolver.ResolveByRelationship(ada, "Accounts");

        Assert.Equal(ResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(AccountId, result.Resolved!.EntityType);
        Assert.Equal("10", result.Resolved.IdentityValue);
    }

    [Fact]
    public void ResolveByRelationship_CustomerWithNoAccounts_IsNotFound()
    {
        var resolver = BuildResolver();
        var grace = resolver.ResolveByIdentity(CustomerId, "2").Resolved!;

        var result = resolver.ResolveByRelationship(grace, "Accounts");

        Assert.Equal(ResolutionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void ResolveByRelationship_UnknownRelationshipName_IsNotFound_NeverInventsOne()
    {
        var resolver = BuildResolver();
        var ada = resolver.ResolveByIdentity(CustomerId, "1").Resolved!;

        var result = resolver.ResolveByRelationship(ada, "Pets");

        Assert.Equal(ResolutionOutcome.NotFound, result.Outcome);
        Assert.Contains("Pets", result.UnresolvedReason);
    }

    [Fact]
    public void ResolveBySearch_MultipleMatches_IsAmbiguous_NotGuessed()
    {
        // Both seeded customers' names fuzzy-match "a" -- Ada Lovelace and
        // Grace Hopper both contain the letter, exercising the ambiguity
        // path instead of the resolver silently picking one.
        var result = BuildResolver().ResolveBySearch(CustomerId, "a");

        Assert.Equal(ResolutionOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Resolved);
        Assert.NotEmpty(result.Evidence);
    }
}
