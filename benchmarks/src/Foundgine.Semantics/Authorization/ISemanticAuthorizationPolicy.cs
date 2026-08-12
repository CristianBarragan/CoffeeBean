using Foundgine.Abstractions;

namespace Foundgine.Semantics.Authorization;

/// <summary>
/// Provider-independent authorization policy for semantic requests.
/// Authorization reasons about domain identities only; it must not inspect
/// SQL tables, columns, joins, providers, or transport-specific concepts.
/// </summary>
public interface ISemanticAuthorizationPolicy
{
    // Existing read hooks remain the compatibility surface for simple policies.
    bool CanAccessEntity(EntityId entityId);
    bool CanAccessField(EntityId entityId, FieldId fieldId);
    bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId);

    // Write access is deliberately opt-in. Existing policies therefore remain
    // read-only until they explicitly grant writes.
    bool CanWriteEntity(EntityId entityId) => false;
    bool CanWriteField(EntityId entityId, FieldId fieldId) => false;
    bool CanWriteRelationship(EntityId sourceEntityId, RelationshipId relationshipId) => false;

    /// <summary>
    /// Optional provider-independent row/field predicate. The predicate is
    /// preserved in the semantic graph and must not be evaluated away before
    /// provider execution. The default is unrestricted access.
    /// </summary>
    AuthorizationPredicate? GetPredicate(EntityId entityId, AuthorizationOperation operation) => null;

    AuthorizationDecision GetEntityAccess(EntityId entityId, AuthorizationOperation operation) =>
        operation == AuthorizationOperation.Read
            ? (CanAccessEntity(entityId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied)
            : (CanWriteEntity(entityId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);

    AuthorizationDecision GetFieldAccess(EntityId entityId, FieldId fieldId, AuthorizationOperation operation) =>
        operation == AuthorizationOperation.Read
            ? (CanAccessField(entityId, fieldId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied)
            : (CanWriteField(entityId, fieldId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);

    AuthorizationDecision GetRelationshipAccess(
        EntityId sourceEntityId,
        RelationshipId relationshipId,
        AuthorizationOperation operation) =>
        operation == AuthorizationOperation.Read
            ? (CanAccessRelationship(sourceEntityId, relationshipId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied)
            : (CanWriteRelationship(sourceEntityId, relationshipId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);
}
