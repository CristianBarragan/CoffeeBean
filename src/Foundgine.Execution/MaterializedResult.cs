using Foundgine.Abstractions;

namespace Foundgine.Execution;

/// <summary>
/// Provider-neutral semantic result tree. Result values contain only fields
/// selected by the execution plan; identity is retained separately so the
/// materializer can reconstruct topology even when an identity field is not
/// part of the requested projection.
/// </summary>
public sealed record MaterializedResult(
    IReadOnlyList<MaterializedNode> Roots,
    ExecutionPageInfo? PageInfo = null,
    ExecutionEvidence? Evidence = null);

public sealed class MaterializedNode
{
    private readonly Dictionary<RelationshipId, List<MaterializedNode>> _children = [];

    public MaterializedNode(
        int planNodeId,
        EntityId entityId,
        object identityValue,
        IReadOnlyDictionary<FieldId, object?> values)
    {
        ArgumentNullException.ThrowIfNull(identityValue);
        PlanNodeId = planNodeId;
        EntityId = entityId;
        IdentityValue = identityValue;
        Values = values;
    }

    public int PlanNodeId { get; }
    public EntityId EntityId { get; }

    /// <summary>
    /// Semantic identity used to collapse repeated provider rows. It is not
    /// implicitly added to <see cref="Values"/> when it was not requested.
    /// </summary>
    public object IdentityValue { get; }

    /// <summary>
    /// Only fields selected by the execution plan.
    /// </summary>
    public IReadOnlyDictionary<FieldId, object?> Values { get; }

    public IReadOnlyDictionary<RelationshipId, IReadOnlyList<MaterializedNode>> Children =>
        _children.ToDictionary(x => x.Key, x => (IReadOnlyList<MaterializedNode>)x.Value);

    internal List<MaterializedNode> GetChildren(RelationshipId relationshipId) =>
        _children.TryGetValue(relationshipId, out var children)
            ? children
            : _children[relationshipId] = [];
}
