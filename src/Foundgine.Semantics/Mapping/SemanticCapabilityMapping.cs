using Foundgine.Abstractions;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Authorization;

namespace Foundgine.Semantics.Mapping;

/// <summary>
/// Consumer-neutral declarative mapping from a semantic schema to a capability
/// implemented by an application method. This is metadata only; it never invokes
/// the mapped method and never performs authorization or execution.
/// </summary>
public sealed record SemanticCapabilityMapping(
    string Id,
    string Schema,
    EntityId TargetEntityId,
    string ImplementationType,
    string MethodName,
    string Operation,
    string? Description = null)
{
    /// <summary>Creates the authoritative semantic capability definition for this mapping.</summary>
    public SemanticCapabilityDefinition ToDefinition(
        AuthorizationDecision access,
        IReadOnlyList<SemanticCapabilityInput>? inputs = null,
        IReadOnlyList<SemanticCapabilityConstraint>? constraints = null,
        IReadOnlyList<SemanticCapabilityEffect>? effects = null,
        IReadOnlyList<SemanticCapabilityAuthorizationRequirement>? authorizationRequirements = null) =>
        new(
            new SemanticCapability(
                Id,
                Description ?? Id,
                TargetEntityId,
                access,
                inputs ?? [],
                constraints ?? [],
                effects ?? [],
                [],
                [])
            {
                Schema = Schema,
                Operation = Operation,
                Metadata = new SemanticCapabilityMetadata(Description),
                Implementation = new SemanticCapabilityImplementation(ImplementationType, MethodName),
                AuthorizationRequirements = authorizationRequirements ?? []
            });
}

/// <summary>Compile-time mapping declarations assembled by the AOT generator.</summary>
public sealed record SemanticSchemaMapping(
    string Name,
    IReadOnlyList<EntityId> EntityIds,
    IReadOnlyList<SemanticCapabilityMapping> Capabilities);

/// <summary>Immutable collection of schema mappings produced by source generation.</summary>
public sealed class SemanticMappingSet
{
    public SemanticMappingSet(IReadOnlyList<SemanticSchemaMapping> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        Schemas = schemas;
    }

    public IReadOnlyList<SemanticSchemaMapping> Schemas { get; }

    public IReadOnlyList<SemanticCapabilityMapping> Capabilities =>
        Schemas.SelectMany(x => x.Capabilities).ToArray();
}
