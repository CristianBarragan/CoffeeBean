namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class CompiledQuery
{
    public required QueryPlan Plan { get; init; }

    public required RowLayout Layout { get; init; }

    public required string Sql { get; init; }

    public required ushort[][] ColumnMaps { get; init; }

    public required ushort RootEntityId { get; init; }
}