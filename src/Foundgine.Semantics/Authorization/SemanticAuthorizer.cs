using Foundgine.Abstractions;
using Foundgine.Semantics.IR;

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


    /// <summary>
    /// Authorizes canonical Semantic IR directly. This is the authoritative
    /// semantic-to-planning authorization boundary: physical providers are
    /// deliberately not involved.
    /// </summary>
    public SemanticOperation Authorize(SemanticOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var root = AuthorizeNode(operation.Root, isRoot: true);
        if (root is null)
            throw new SemanticAuthorizationException(
                $"Access denied for entity '{operation.Root.EntityId}'.");

        return new SemanticOperation(root);
    }

    private SemanticReadNode? AuthorizeNode(SemanticReadNode node, bool isRoot)
    {
        var entity = _policy.GetEntityAccess(node.EntityId, AuthorizationOperation.Read);
        var predicate = _policy.GetPredicate(node.EntityId, AuthorizationOperation.Read);

        var effective = AuthorizationDecision.Combine(
            entity,
            predicate is null
                ? AuthorizationDecision.Allowed
                : AuthorizationDecision.Conditional(predicate));

        // A denied entity removes a child subtree; a denied root rejects the
        // operation. This mirrors graph authorization while keeping the
        // canonical Semantic IR as the authorization boundary.
        if (!effective.IsAllowed)
        {
            if (isRoot)
                throw new SemanticAuthorizationException(
                    $"Access denied for entity '{node.EntityId}'.");
            return null;
        }

        var fieldDecisions = node.Fields
            .Distinct()
            .Select(fieldId => (
                FieldId: fieldId,
                Decision: _policy.GetFieldAccess(
                    node.EntityId, fieldId, AuthorizationOperation.Read)))
            .ToArray();

        var fields = new List<FieldId>(fieldDecisions.Length);
        foreach (var field in fieldDecisions)
        {
            if (!field.Decision.IsAllowed)
                continue;

            fields.Add(field.FieldId);
            effective = AuthorizationDecision.Combine(effective, field.Decision);
        }

        if (node.Authorization is not null)
            effective = AuthorizationDecision.Combine(
                effective,
                AuthorizationDecision.Conditional(node.Authorization));

        if (!effective.IsAllowed)
        {
            if (isRoot)
                throw new SemanticAuthorizationException(
                    $"Authorization constraints denied semantic node '{node.Id}'.");
            return null;
        }

        var children = new List<SemanticReadNode>();
        foreach (var child in node.Children)
        {
            if (child.ViaRelationship is { } relationshipId)
            {
                var relationship = _policy.GetRelationshipAccess(
                    node.EntityId, relationshipId, AuthorizationOperation.Read);

                if (!relationship.IsAllowed)
                    continue;

                var childAuthorized = AuthorizeNode(child, isRoot: false);
                if (childAuthorized is null)
                    continue;

                var combined = AuthorizationDecision.Combine(
                    relationship,
                    childAuthorized.Authorization is null
                        ? AuthorizationDecision.Allowed
                        : AuthorizationDecision.Conditional(childAuthorized.Authorization));

                children.Add(childAuthorized with
                {
                    Authorization = combined.Predicate
                });
                continue;
            }

            var authorizedChild = AuthorizeNode(child, isRoot: false);
            if (authorizedChild is not null)
                children.Add(authorizedChild);
        }

        return node with
        {
            Fields = fields.ToArray(),
            Children = children,
            Authorization = effective.Predicate
        };
    }

    private static AuthorizationDecision AuthorizationDecisionFromPredicate(AuthorizationPredicate? predicate) =>
        predicate is null
            ? AuthorizationDecision.Allowed
            : AuthorizationDecision.Conditional(predicate);
}

