#nullable enable

using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class SqlCompilationContext
{
    public string SelectSql { get; set; } = string.Empty;

    public string UpsertSql { get; set; } = string.Empty;

    public string SqlWhereStatement { get; set; } = string.Empty;


    public EntityFilterMetadata? Filter { get; set; }


    // public QueryPagination Pagination { get; set; } = new();


    public SqlCompilationContext()
    {
    }
}