using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

public sealed record JoinedRow(
    IReadOnlyDictionary<ushort, EntityRow> Entities
);

public sealed record EntityRow(
    ushort EntityId,
    object?[] Columns
);

public sealed record ExecutionResult(
    IReadOnlyList<JoinedRow> Rows
);

