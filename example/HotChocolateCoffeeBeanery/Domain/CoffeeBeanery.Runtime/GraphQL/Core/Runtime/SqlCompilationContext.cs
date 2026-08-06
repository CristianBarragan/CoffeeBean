// using CoffeeBeanery.GraphQL.Core.Sql;
// using ModelNodeTree = CoffeeBeanery.GraphQL.Core.Sql.ModelNodeTree;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public class SqlCompilationContext
// {
//     public string SelectSql { get; set; }
//     public string UpsertSql { get; set; }
//     public string SqlWhereStatement { get; set; }
//     public List<string> SqlOrderStatements { get; set; } = new();
//     
//     public QueryPlan QueryPlan { get; set; }
//
//     public Pagination Pagination { get; set; }
//     public bool HasTotalCount { get; set; }
// }