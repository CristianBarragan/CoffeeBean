using Foundgine.Abstractions;

namespace Foundgine.Semantics.Authorization;

/// <summary>
/// Provider-independent authorization policy for semantic requests.
/// Authorization reasons about domain identities only; it must not inspect
/// SQL tables, columns, joins, providers, or transport-specific concepts.
/// </summary>
public interface ISemanticAuthorizationPolicy
{
    bool CanAccessEntity(EntityId entityId);

    bool CanAccessField(EntityId entityId, FieldId fieldId);

    bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId);
}
