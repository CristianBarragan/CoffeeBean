using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// The semantic description of one domain entity -- identity, fields,
/// relationships, search capability, actions, and policies -- with no
/// knowledge of how it's stored or transported. This is the type Milestone
/// 1's acceptance test enumerates:
///
/// <code>
/// Customer
///  ├── identity: Id
///  ├── fields: Name
///  ├── relationship: Accounts
///  └── actions: &lt;none initially&gt;
/// </code>
///
/// Built by hand (via <see cref="SemanticModelBuilder"/>) today; a future
/// compile-time domain compiler (Milestone 10) is expected to emit these
/// same shapes rather than a bespoke planner of its own.
/// </summary>
public sealed record SemanticEntity(
    EntityId Id,
    string Name,
    SemanticIdentity Identity,
    IReadOnlyList<SemanticField> Fields,
    IReadOnlyList<SemanticRelationship> Relationships,
    IReadOnlyList<ActionDescriptor> Actions,
    SearchCapability? Search = null,
    IReadOnlyList<PolicyDescriptor>? Policies = null)
{
    /// <summary>Never null, even when no policies were declared.</summary>
    public IReadOnlyList<PolicyDescriptor> EffectivePolicies => Policies ?? [];
}
