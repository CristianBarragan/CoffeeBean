using ModelNodeTree = CoffeeBeanery.GraphQL.Core.Sql.ModelNodeTree;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public class _SqlCompilationContext
{
    public string SelectSql { get; set; }
    public string UpsertSql { get; set; }
    public string SqlWhereStatement { get; set; }
    public List<string> SqlOrderStatements { get; set; } = new();
    public string SplitOnDapper { get; set; }
    public object Pagination { get; set; }
    public bool HasTotalCount { get; set; }
}