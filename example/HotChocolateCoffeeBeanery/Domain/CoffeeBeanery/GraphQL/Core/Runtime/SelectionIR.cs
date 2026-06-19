using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// A single scalar field selected on an entity.
/// FieldId is a generated ushort constant (IdEmitter output).
/// OutputAlias is the wire alias the client used, already resolved
/// by the adapter from HotChocolate's alias → schema name mapping.
/// </summary>
public readonly struct ScalarSelection
{
    public readonly ushort FieldId;
    public readonly string OutputAlias;

    public ScalarSelection(ushort fieldId, string outputAlias)
    {
        FieldId = fieldId;
        OutputAlias = outputAlias;
    }
}

/// <summary>
/// A node in the normalized selection tree.
/// EntityId is a generated ushort constant (IdEmitter output).
/// OutputAlias is the role alias the client used for this entity
/// (e.g. "InnerCustomer" when the schema name is "Customer").
/// Children are nested entity selections (not scalar fields).
///
/// Invariants guaranteed by the adapter:
///   - No HotChocolate types leak past this struct.
///   - Field aliases are already resolved to schema names.
///   - Inline fragments are already unwrapped.
///   - Conditional fields (@skip/@include) are marked IsConditional
///     but NOT removed here; Selection Optimizer handles that.
/// </summary>
public readonly struct SelectionIR
{
    public readonly ushort EntityId;
    public readonly string OutputAlias;
    public readonly bool IsConditional;
    public readonly ImmutableArray<ScalarSelection> Scalars;
    public readonly ImmutableArray<SelectionIR> Children;

    public SelectionIR(
        ushort entityId,
        string outputAlias,
        bool isConditional,
        ImmutableArray<ScalarSelection> scalars,
        ImmutableArray<SelectionIR> children)
    {
        EntityId = entityId;
        OutputAlias = outputAlias;
        IsConditional = isConditional;
        Scalars = scalars;
        Children = children;
    }

    public static readonly SelectionIR Empty = new(
        0, string.Empty, false,
        ImmutableArray<ScalarSelection>.Empty,
        ImmutableArray<SelectionIR>.Empty);
}