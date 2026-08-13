using Foundgine.Abstractions;

namespace Foundgine.Semantics.Authorization;

/// <summary>
/// Applies authorization to an already-resolved semantic graph.
/// The result remains provider- and protocol-independent.
/// </summary>
public sealed class SemanticAuthorizer
{
    private readonly ISemanticAuthorizationPolicy _policy;

    public SemanticAuthorizer(ISemanticAuthorizationPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public SemanticGraph Authorize(SemanticGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Nodes.Count == 0)
            return graph;

        var authorized = new SemanticGraph { Options = graph.Options };
        var sourceToAuthorized = new Dictionary<int, SemanticGraphNode>();

        foreach (var sourceNode in graph.Nodes)
        {
            SemanticGraphNode? authorizedParent = null;
            if (sourceNode.ParentId is not null &&
                !sourceToAuthorized.TryGetValue(sourceNode.ParentId.Value, out authorizedParent))
            {
                // An ancestor was denied, so this node is unreachable.
                continue;
            }

            var fieldDecisions = sourceNode.Fields
                .Distinct()
                .Select(fieldId => (
                    FieldId: fieldId,
                    Decision: _policy.GetFieldAccess(
                        sourceNode.EntityId, fieldId, AuthorizationOperation.Read)))
                .ToArray();

            var fields = fieldDecisions
                .Where(x => x.Decision.IsAllowed)
                .Select(x => x.FieldId)
                .ToArray();

            var fieldAuthorization = AuthorizationDecision.Allowed;
            foreach (var decision in fieldDecisions.Where(x => x.Decision.IsAllowed))
                fieldAuthorization = AuthorizationDecision.Combine(fieldAuthorization, decision.Decision);

            var authorization = AuthorizationDecision.Combine(
                _policy.GetEntityAccess(sourceNode.EntityId, AuthorizationOperation.Read),
                AuthorizationDecisionFromPredicate(_policy.GetPredicate(
                    sourceNode.EntityId, AuthorizationOperation.Read)));
            authorization = AuthorizationDecision.Combine(authorization, fieldAuthorization);

            if (!authorization.IsAllowed)
            {
                if (sourceNode.ParentId is null)
                    throw new SemanticAuthorizationException(
                        $"Access denied for entity '{sourceNode.EntityId}'.");

                continue;
            }

            if (sourceNode.ViaRelationship is { } relationshipId)
            {
                if (sourceNode.ParentId is not { } parentId ||
                    !graph.Nodes.Any(node => node.Id == parentId))
                {
                    throw new InvalidOperationException(
                        $"Graph node {sourceNode.Id} has relationship '{relationshipId}' but no valid parent.");
                }

                var parentEntityId = graph.Nodes.Single(node => node.Id == parentId).EntityId;
                var relationshipDecision = _policy.GetRelationshipAccess(
                    parentEntityId, relationshipId, AuthorizationOperation.Read);
                if (!relationshipDecision.IsAllowed)
                {
                    // A denied relationship removes this node and its descendants.
                    continue;
                }

                authorization = AuthorizationDecision.Combine(authorization, relationshipDecision);
            }

            var sourcePredicate = sourceNode.Authorization;
            if (sourcePredicate is not null && authorization.Predicate != sourcePredicate)
            {
                authorization = AuthorizationDecision.Combine(
                    authorization,
                    AuthorizationDecisionFromPredicate(sourcePredicate));
            }

            var predicate = authorization.Predicate;
            var node = sourceNode.ParentId is null
                ? authorized.AddRoot(sourceNode.EntityId, fields, predicate)
                : authorized.Add(
                    sourceNode.EntityId,
                    sourceNode.ViaRelationship,
                    authorizedParent,
                    fields,
                    predicate);

            sourceToAuthorized[sourceNode.Id] = node;
        }

        return authorized;
    }

    private static AuthorizationDecision AuthorizationDecisionFromPredicate(AuthorizationPredicate? predicate) =>
        predicate is null
            ? AuthorizationDecision.Allowed
            : AuthorizationDecision.Conditional(predicate);
}

