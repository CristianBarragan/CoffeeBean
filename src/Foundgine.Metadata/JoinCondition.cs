namespace Foundgine.Metadata;

public sealed record JoinCondition(
    ColumnReference Left,
    ColumnReference Right);
