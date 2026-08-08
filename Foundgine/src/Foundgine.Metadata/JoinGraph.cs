namespace Foundgine.Metadata;

public sealed class JoinGraph
{
    private readonly Dictionary<(EntityId From, EntityId To), JoinMetadata> _edges = new();

    public void AddEdge(EntityId from, EntityId to, JoinMetadata join)
    {
        _edges[(from, to)] = join;

        // Also index the reverse direction so callers don't need to know
        // which side is "dependent" vs "principal" to find a path.
        var reversed = new JoinMetadata(
            new JoinCondition(join.Condition.Right, join.Condition.Left),
            join.Kind);

        _edges.TryAdd((to, from), reversed);
    }

    public bool TryGetJoin(EntityId from, EntityId to, out JoinMetadata join) =>
        _edges.TryGetValue((from, to), out join!);

    public IEnumerable<(EntityId From, EntityId To, JoinMetadata Join)> EdgesFrom(EntityId from) =>
        _edges.Where(e => e.Key.From == from).Select(e => (e.Key.From, e.Key.To, e.Value));
}
