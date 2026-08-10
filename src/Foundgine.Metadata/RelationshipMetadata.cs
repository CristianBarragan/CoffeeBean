namespace Foundgine.Metadata;

/// <summary>
/// Static relationship mapping between two domain entities. Cardinality is
/// intentionally semantic and therefore belongs in Foundgine.Semantics.
/// </summary>
public sealed record RelationshipMetadata(
    RelationshipId Id,
    EntityId Source,
    EntityId Target,
    string Name);
