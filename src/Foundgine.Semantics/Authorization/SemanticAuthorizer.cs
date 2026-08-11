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
            if (!_policy.CanAccessEntity(sourceNode.EntityId))
            {
                if (sourceNode.ParentId is null)
                    throw new SemanticAuthorizationException(
                        $"Access denied for entity '{sourceNode.EntityId}'.");

                // A denied entity removes its entire subtree.
                continue;
            }

            SemanticGraphNode? authorizedParent = null;
            if (sourceNode.ParentId is not null &&
                !sourceToAuthorized.TryGetValue(sourceNode.ParentId.Value, out authorizedParent))
            {
                // An ancestor was denied, so this node is unreachable.
                continue;
            }

            var fields = sourceNode.Fields
                .Where(fieldId => _policy.CanAccessField(sourceNode.EntityId, fieldId))
                .Distinct()
                .ToArray();

            if (sourceNode.ViaRelationship is { } relationshipId)
            {
                if (sourceNode.ParentId is not { } parentId ||
                    !graph.Nodes.Any(node => node.Id == parentId))
                {
                    throw new InvalidOperationException(
                        $"Graph node {sourceNode.Id} has relationship '{relationshipId}' but no valid parent.");
                }

                var parentEntityId = graph.Nodes.Single(node => node.Id == parentId).EntityId;
                if (!_policy.CanAccessRelationship(parentEntityId, relationshipId))
                {
                    // A denied relationship removes this node and its descendants.
                    continue;
                }
            }

            var node = sourceNode.ParentId is null
                ? authorized.AddRoot(sourceNode.EntityId, fields, sourceNode.Authorization)
                : authorized.Add(
                    sourceNode.EntityId,
                    sourceNode.ViaRelationship,
                    authorizedParent,
                    fields,
                    sourceNode.Authorization);

            sourceToAuthorized[sourceNode.Id] = node;
        }

        return authorized;
    }
}

