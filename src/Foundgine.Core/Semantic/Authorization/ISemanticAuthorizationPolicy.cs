using Foundgine.Core.Abstractions;

namespace Foundgine.Core.Semantic.Authorization;

/// <summary>
///     Provider-independent authorization policy for semantic requests.
///     Authorization reasons about domain identities only; it must not inspect
///     SQL tables, columns, joins, providers, or transport-specific concepts.
/// </summary>
public interface ISemanticAuthorizationPolicy
{
    // Existing read hooks remain the compatibility surface for simple policies.
    bool CanAccessEntity(EntityId entityId);
    bool CanAccessField(EntityId entityId, FieldId fieldId);
    bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId);

    // Write access is deliberately opt-in. Existing policies therefore remain
    // read-only until they explicitly grant writes.
    bool CanWriteEntity(EntityId entityId)
    {
        return false;
    }

    bool CanWriteField(EntityId entityId, FieldId fieldId)
    {
        return false;
    }

    bool CanWriteRelationship(EntityId sourceEntityId, RelationshipId relationshipId)
    {
        return false;
    }

    /// <summary>
    ///     Optional provider-independent row/field predicate. The predicate is
    ///     preserved in the semantic graph and must not be evaluated away before
    ///     provider execution. The default is unrestricted access.
    /// </summary>
    AuthorizationPredicate? GetPredicate(EntityId entityId, AuthorizationOperation operation)
    {
        return null;
    }

    AuthorizationDecision GetEntityAccess(EntityId entityId, AuthorizationOperation operation)
    {
        return operation == AuthorizationOperation.Read
            ? (CanAccessEntity(entityId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied)
            : (CanWriteEntity(entityId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);
    }

    /// <summary>
    ///     Named-operation refinement of <see cref="GetEntityAccess(EntityId, AuthorizationOperation)" />.
    ///     The default falls back to the coarse Read/Write decision above, so
    ///     existing policies keep their current behavior unchanged. Override this
    ///     only when a policy needs to distinguish domain-specific write intents
    ///     (for example "Invoice.Pay" versus "Invoice.Update") that the coarse
    ///     <see cref="AuthorizationOperation.Write" /> gate does not separate.
    ///     A denial here must never be weaker than the coarse decision would be:
    ///     this refinement may only narrow access, never widen it.
    /// </summary>
    AuthorizationDecision GetEntityAccess(
        EntityId entityId,
        AuthorizationOperation operation,
        AuthorizationOperationName? name)
    {
        return GetEntityAccess(entityId, operation);
    }

    AuthorizationDecision GetFieldAccess(EntityId entityId, FieldId fieldId, AuthorizationOperation operation)
    {
        return operation == AuthorizationOperation.Read
            ? (CanAccessField(entityId, fieldId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied)
            : (CanWriteField(entityId, fieldId) ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);
    }

    AuthorizationDecision GetRelationshipAccess(
        EntityId sourceEntityId,
        RelationshipId relationshipId,
        AuthorizationOperation operation)
    {
        return operation == AuthorizationOperation.Read
            ? (CanAccessRelationship(sourceEntityId, relationshipId)
                ? AuthorizationDecision.Allowed
                : AuthorizationDecision.Denied)
            : (CanWriteRelationship(sourceEntityId, relationshipId)
                ? AuthorizationDecision.Allowed
                : AuthorizationDecision.Denied);
    }
}