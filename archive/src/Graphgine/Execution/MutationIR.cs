using System.Collections.Immutable;

namespace Graphgine.Execution;

/// <summary>
/// A scalar field value supplied in a mutation input.
/// FieldId is a generated ushort constant.
/// RawValue is always a string; the SQL writer handles quoting/casting.
/// </summary>
public readonly struct FieldValue
{
    public readonly ushort EntityId;
    public readonly ushort FieldId;
    public readonly ushort ColumnId;
    public readonly string RawValue;

    public FieldValue(ushort entityId,
        ushort fieldId,
        ushort columnId,
        string rawValue)
    {
        EntityId = entityId;
        FieldId = fieldId;
        ColumnId = columnId;
        RawValue = rawValue;
    }
    
    public FieldValue(ushort fieldId,
        ushort columnId,
        string rawValue)
    {
        FieldId = fieldId;
        ColumnId = columnId;
        RawValue = rawValue;
    }
    
    public FieldValue(ushort fieldId, string rawValue)
    {
        FieldId = fieldId;
        RawValue = rawValue;
    }
}

/// <summary>
/// A node in the normalized mutation input tree.
/// Mirrors SelectionIR's shape so the same planner switch structure
/// handles both query and mutation in parallel generated methods.
/// </summary>
public readonly struct MutationIR
{
    public readonly ushort EntityId;
    public readonly string OutputAlias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly ImmutableArray<MutationIR> Children;

    public MutationIR(
        ushort entityId,
        string outputAlias,
        ImmutableArray<FieldValue> values,
        ImmutableArray<MutationIR> children)
    {
        EntityId = entityId;
        OutputAlias = outputAlias;
        Values = values;
        Children = children;
    }

    public static readonly MutationIR Empty = new(
        0, string.Empty,
        ImmutableArray<FieldValue>.Empty,
        ImmutableArray<MutationIR>.Empty);
}