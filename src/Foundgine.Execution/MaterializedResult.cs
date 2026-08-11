using Foundgine.Abstractions;

namespace Foundgine.Execution;

public sealed record MaterializedResult(IReadOnlyList<MaterializedNode> Roots);

public sealed class MaterializedNode
{
    private readonly Dictionary<RelationshipId, List<MaterializedNode>> _children = [];

    public MaterializedNode(int planNodeId, EntityId entityId, IReadOnlyDictionary<FieldId, object?> values)
    {
        PlanNodeId = planNodeId;
        EntityId = entityId;
        Values = values;
    }

    public int PlanNodeId { get; }
    public EntityId EntityId { get; }
    public IReadOnlyDictionary<FieldId, object?> Values { get; }

    public IReadOnlyDictionary<RelationshipId, IReadOnlyList<MaterializedNode>> Children =>
        _children.ToDictionary(x => x.Key, x => (IReadOnlyList<MaterializedNode>)x.Value);

    internal List<MaterializedNode> GetChildren(RelationshipId relationshipId) =>
        _children.TryGetValue(relationshipId, out var children)
            ? children
            : _children[relationshipId] = [];
}
