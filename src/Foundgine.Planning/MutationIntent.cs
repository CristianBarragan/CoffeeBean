using Foundgine.Builders;
using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// One column's input value for a mutation: a <see cref="ColumnId"/> on the
/// target entity plus the literal to write. This is the mutation
/// counterpart of <see cref="ComparisonFilter.Value"/> on the read side —
/// <see cref="MutationPlanner"/> is what turns it into a fully-resolved
/// <see cref="Foundgine.Builders.MutationColumn"/> (via
/// <see cref="Foundgine.Metadata.MutationColumn"/>'s <c>Value</c>) against
/// real <see cref="Foundgine.Metadata"/>.
/// </summary>
public sealed record MutationFieldValue(
    ushort ColumnId,
    object? Value
);

/// <summary>
/// What a caller wants written, expressed purely in terms of
/// <see cref="Foundgine.Metadata"/> identities — never in terms of tables,
/// SQL, or any other physical concept. <see cref="MutationPlanner"/> is the
/// only thing that turns this into a <see cref="Foundgine.Builders.MutationPlan"/>,
/// the same relationship <see cref="QueryIntent"/> has to
/// <see cref="QueryPlanner"/>/<see cref="Foundgine.Builders.QueryPlan"/>.
///
/// <see cref="Filter"/> identifies the target row(s) for
/// <see cref="Foundgine.Builders.MutationKind.Update"/> and
/// <see cref="Foundgine.Builders.MutationKind.Delete"/> — it is required for
/// both (see <see cref="MutationPlanner.Plan"/>), since Foundgine never
/// mutates every row by accident. It is ignored for
/// <see cref="Foundgine.Builders.MutationKind.Create"/>, which always
/// inserts a new row from <see cref="Fields"/>.
/// </summary>
public sealed record MutationIntent(
    EntityId Entity,
    MutationKind Kind,
    IReadOnlyList<MutationFieldValue> Fields,
    FilterExpression? Filter = null
);
