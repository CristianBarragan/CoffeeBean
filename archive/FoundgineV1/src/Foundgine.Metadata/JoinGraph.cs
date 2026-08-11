namespace Foundgine.Metadata;

public sealed class JoinGraph
{
    private readonly Dictionary<(EntityId From, EntityId To), JoinMetadata> _edges = new();

    public void AddEdge(EntityId from, EntityId to, JoinMetadata join)
    {
        _edges[(from, to)] = join;

        // Also index the reverse direction so callers don't need to know
        // which side is "dependent" vs "principal" to find a path.
        //
        // Reversing which side leads which also reverses the *meaning* of
        // Left/Right: "A LEFT JOIN B" read from B's side is "B RIGHT JOIN
        // A", not "B LEFT JOIN A". Inner and Full are symmetric and stay as
        // they are; only Left/Right need to swap.
        var reversed = new JoinMetadata(
            new JoinCondition(join.Condition.Right, join.Condition.Left),
            ReverseKind(join.Kind));

        _edges.TryAdd((to, from), reversed);
    }

    private static JoinKind ReverseKind(JoinKind kind) => kind switch
    {
        JoinKind.Left => JoinKind.Right,
        JoinKind.Right => JoinKind.Left,
        JoinKind.Inner => JoinKind.Inner,
        JoinKind.Full => JoinKind.Full,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown JoinKind."),
    };

    public bool TryGetJoin(EntityId from, EntityId to, out JoinMetadata join) =>
        _edges.TryGetValue((from, to), out join!);

    public IEnumerable<(EntityId From, EntityId To, JoinMetadata Join)> EdgesFrom(EntityId from) =>
        _edges.Where(e => e.Key.From == from).Select(e => (e.Key.From, e.Key.To, e.Value));
}