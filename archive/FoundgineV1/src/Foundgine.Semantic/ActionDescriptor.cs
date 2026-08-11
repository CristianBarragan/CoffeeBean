using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// An explicit business operation exposed on an entity -- e.g.
/// IssueRefund, SuspendAccount. This is the *only* vocabulary an agent
/// (Milestone 4) is allowed to act through: it can select an
/// <see cref="ActionDescriptor"/> the semantic model exposes, and it
/// cannot invent an arbitrary method call.
///
/// The shape mirrors Milestone 4's descriptor list exactly (Name, Target
/// entity, Inputs, Mutating?, Authorization requirements, Side effects,
/// Verification requirements) so that when Milestone 4 lands, actions
/// slot into the model built here instead of requiring a new type.
/// A Milestone-1 entity typically has zero actions -- see
/// <see cref="ActionDescriptor.NonMutating"/> for the simplest possible
/// non-mutating action, and <see cref="SemanticEntityBuilder"/> for how an
/// entity's action list stays empty by default.
/// </summary>
public sealed record ActionDescriptor(
    string Name,
    EntityId Target,
    IReadOnlyList<ActionParameter> Inputs,
    bool IsMutating,
    IReadOnlyList<string> AuthorizationRequirements,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<string> VerificationRequirements)
{
    /// <summary>
    /// Convenience factory for a read-only action with no authorization,
    /// side-effect, or verification requirements -- useful for tests and
    /// early samples before Milestone 5 (policy) is wired up.
    /// </summary>
    public static ActionDescriptor NonMutating(string name, EntityId target, params ActionParameter[] inputs) =>
        new(name, target, inputs, false, [], [], []);
}
