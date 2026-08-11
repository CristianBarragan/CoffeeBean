using Foundgine.Abstractions;

namespace Foundgine.Semantics.Authorization;

/// <summary>Default policy used when no authorization restrictions are configured.</summary>
public class AllowAllSemanticAuthorizationPolicy : ISemanticAuthorizationPolicy
{
    public virtual bool CanAccessEntity(EntityId entityId) => true;

    public virtual bool CanAccessField(EntityId entityId, FieldId fieldId) => true;

    public virtual bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId) => true;
}
