namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class FieldResolution
{
    public string? ChildAlias { get; init; }
    public List<(string EntityAlias, string EntityColumn)> Columns { get; init; }
    public Dictionary<string, int>? EnumMap { get; init; }
}