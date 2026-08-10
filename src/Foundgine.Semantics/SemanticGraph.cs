using Foundgine.Metadata;

namespace Foundgine.Semantics;

/// <summary>
/// The canonical request graph. It contains only semantic/domain topology.
/// It must not contain SQL, GraphQL, provider nodes, aliases, or storage SQL.
/// </summary>
public sealed class SemanticGraph
{
    private readonly List<SemanticGraphNode> _nodes = [];

    public IReadOnlyList<SemanticGraphNode> Nodes => _nodes;

    public SemanticGraphNode AddRoot(EntityId entityId) =>
        Add(entityId, null, null);

    public SemanticGraphNode Add(
        EntityId entityId,
        RelationshipId? relationshipId,
        SemanticGraphNode? parent)
    {
        var node = new SemanticGraphNode(
            _nodes.Count,
            entityId,
            relationshipId,
            parent?.Id);

        _nodes.Add(node);
        return node;
    }
}

public sealed record SemanticGraphNode(
    int Id,
    EntityId EntityId,
    RelationshipId? ViaRelationship,
    int? ParentId);
