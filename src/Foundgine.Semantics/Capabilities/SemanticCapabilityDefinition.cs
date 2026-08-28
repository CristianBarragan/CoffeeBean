using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;

namespace Foundgine.Semantics.Capabilities;

/// <summary>
/// The single provider-neutral definition of a capability. The semantic
/// contract carries authorization, constraints and effects; the optional
/// implementation binding identifies the application entry point. Adapters
/// such as Agent Framework, MCP and GraphQL must project this definition rather
/// than create another capability model.
/// </summary>
public sealed record SemanticCapabilityDefinition(SemanticCapability Capability)
{
    public string Id => Capability.Id;
    public string QualifiedName => Capability.QualifiedName;
    public string Schema => Capability.Schema;
    public EntityId TargetEntityId => Capability.TargetEntityId;
    public AuthorizationDecision Authorization => Capability.Access;
    public IReadOnlyList<SemanticCapabilityInput> Inputs => Capability.Inputs;
    public IReadOnlyList<SemanticCapabilityConstraint> Constraints => Capability.Constraints;
    public IReadOnlyList<SemanticCapabilityEffect> Effects => Capability.Effects;
    public IReadOnlyList<SemanticCapabilityAuthorizationRequirement> AuthorizationRequirements
        => Capability.AuthorizationRequirements;
    public SemanticCapabilityImplementation? Implementation => Capability.Implementation;
    public SemanticCapabilityMetadata Metadata => Capability.Metadata;

    public static SemanticCapabilityDefinition From(SemanticCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return new(capability);
    }
}

