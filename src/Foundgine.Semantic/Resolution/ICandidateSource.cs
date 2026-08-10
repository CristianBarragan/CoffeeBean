using Foundgine.Metadata;

namespace Foundgine.Semantic.Resolution;

/// <summary>
/// The one place real data enters resolution. <see cref="EntityResolver"/>
/// never invents an identity -- Milestone 2's hard rule -- so it can only
/// return what an <see cref="ICandidateSource"/> actually reports finding.
/// Every method here returns a list rather than a single value on purpose:
/// deciding what zero, one, or many candidates mean is the resolver's job,
/// not the source's.
///
/// <see cref="Foundgine.Semantic"/> is not allowed a <c>ProjectReference</c>
/// on <c>Foundgine.Execution.Contracts</c> or <c>Foundgine.Providers</c>
/// (see <c>ArchitectureTests</c>), so this interface is the seam that lets
/// a real implementation -- e.g. a SQLite-backed source in a sample, or
/// something reading through <c>Foundgine.Providers</c> later -- sit
/// outside this project entirely while resolution stays provider-agnostic.
/// </summary>
public interface ICandidateSource
{
    /// <summary>Look up one entity instance by its identity field's literal value, e.g. Account "10".</summary>
    IReadOnlyList<IdentityCandidate> FindByIdentity(EntityId entityType, string identityValue);

    /// <summary>Search free text against one searchable field, e.g. Customer.Name for "Ada Lovelace".</summary>
    IReadOnlyList<IdentityCandidate> FindByField(
        EntityId entityType,
        FieldId fieldId,
        string text,
        SearchStrategy strategy);

    /// <summary>Walk a relationship from an already-resolved source instance, e.g. Customer "1" -> Accounts.</summary>
    IReadOnlyList<IdentityCandidate> FindByRelationship(RelationshipId relationshipId, string sourceIdentityValue);

    /// <summary>
    /// Walk a relationship the same way <see cref="FindByRelationship"/> does, but ordered by one
    /// field on the target entity and capped to <paramref name="limit"/> -- e.g. Account "10" -&gt;
    /// Transactions, ordered by TransactionDate descending, limit 1, for "her last transaction".
    ///
    /// This exists for <see cref="Intent.ActionPlanner"/>'s target selection (Milestone 4 -- Actions):
    /// an action like IssueRefund is often expressed against "the last transaction", not an
    /// explicit identity, and picking that deterministically requires real ordering a plain
    /// unordered <see cref="FindByRelationship"/> candidate list can't provide.
    ///
    /// Default implementation falls back to <see cref="FindByRelationship"/>, truncated to
    /// <paramref name="limit"/>, in whatever order the source already returns -- correct enough to
    /// compile against, but not actually "last" anything. A source that wants
    /// <see cref="Intent.ActionPlanner"/>'s target selection to be meaningful (rather than just
    /// arbitrary) must override this with a real ordered lookup, e.g. a SQL-backed source doing a
    /// real <c>ORDER BY ... LIMIT</c>.
    /// </summary>
    IReadOnlyList<IdentityCandidate> FindByRelationshipOrdered(
        RelationshipId relationshipId,
        string sourceIdentityValue,
        FieldId orderBy,
        bool descending,
        int limit) =>
        FindByRelationship(relationshipId, sourceIdentityValue).Take(limit).ToList();
}
