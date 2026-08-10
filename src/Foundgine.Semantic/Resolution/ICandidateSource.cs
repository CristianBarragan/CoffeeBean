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
}
