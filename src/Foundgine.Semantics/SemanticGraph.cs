using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// The canonical request graph. It contains only semantic/domain topology.
/// It must not contain SQL, GraphQL, provider nodes, aliases, or storage SQL.
/// </summary>
public sealed class SemanticGraph
{
    private readonly List<SemanticGraphNode> _nodes = [];

    public IReadOnlyList<SemanticGraphNode> Nodes => _nodes;

    public Foundgine.Semantics.Query.SemanticQueryOptions? Options { get; internal set; }

    public SemanticGraphNode AddRoot(
        EntityId entityId,
        IEnumerable<FieldId>? fields = null) =>
        Add(entityId, null, null, fields);

    public SemanticGraphNode Add(
        EntityId entityId,
        RelationshipId? relationshipId,
        SemanticGraphNode? parent,
        IEnumerable<FieldId>? fields = null)
    {
        var node = new SemanticGraphNode(
            _nodes.Count,
            entityId,
            relationshipId,
            parent?.Id)
        {
            Fields = fields?.Distinct().ToArray() ?? []
        };

        _nodes.Add(node);
        return node;
    }
}

public sealed record SemanticGraphNode(
    int Id,
    EntityId EntityId,
    RelationshipId? ViaRelationship,
    int? ParentId)
{
    /// <summary>
    /// Fields selected for this entity in the request. These are semantic
    /// field identities only; no provider/storage information is carried.
    /// </summary>
    public IReadOnlyList<FieldId> Fields { get; init; } = [];
}
