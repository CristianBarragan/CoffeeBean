namespace Foundgine.Metadata;

public sealed record JoinMetadata(
    JoinCondition Condition,
    JoinKind Kind
);
