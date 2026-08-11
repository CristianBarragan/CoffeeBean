using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// The canonical request graph. It contains only semantic/domain topology.
/// It must not contain SQL, GraphQL, provider nodes, aliases, or storage SQL.
/// A node may be reached through either a relational relationship or an AOT
/// semantic connection. A connection is a pre-resolved communication edge.
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

    /// <summary>
    /// Adds a node reached through an AOT semantic connection. The connection
    /// identifies the pre-resolved communication edge; the target entity is
    /// already known and remains the only storage-side identity in the graph.
    /// </summary>
    public SemanticGraphNode AddConnection(
        EntityId entityId,
        ConnectionId connectionId,
        SemanticGraphNode parent,
        IEnumerable<FieldId>? fields = null,
        AuthorizationPredicate? authorization = null) =>
        Add(entityId, null, connectionId, parent, fields, authorization);

    public SemanticGraphNode Add(
        EntityId entityId,
        RelationshipId? relationshipId,
        SemanticGraphNode? parent,
        IEnumerable<FieldId>? fields = null) =>
        Add(entityId, relationshipId, null, parent, fields, null);

    private SemanticGraphNode Add(
        EntityId entityId,
        RelationshipId? relationshipId,
        ConnectionId? connectionId,
        SemanticGraphNode? parent,
        IEnumerable<FieldId>? fields = null,
        AuthorizationPredicate? authorization = null)
    {
        if (relationshipId is not null && connectionId is not null)
            throw new ArgumentException("A semantic node cannot be reached through both a relationship and a connection.");

        if (parent is null && connectionId is not null)
            throw new ArgumentException("A root semantic node cannot be reached through a connection.");

        var node = new SemanticGraphNode(
            _nodes.Count,
            entityId,
            relationshipId,
            connectionId,
            parent?.Id,
            authorization)
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
    ConnectionId? ViaConnection,
    int? ParentId,
    AuthorizationPredicate? Authorization = null)
{
    /// <summary>
    /// Fields selected for this entity in the request. These are semantic
    /// field identities only; no provider/storage information is carried.
    /// </summary>
    public IReadOnlyList<FieldId> Fields { get; init; } = [];
}
