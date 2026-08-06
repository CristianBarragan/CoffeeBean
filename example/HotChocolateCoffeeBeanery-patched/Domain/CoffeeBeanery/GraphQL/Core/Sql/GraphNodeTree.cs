// using CoffeeBeanery.GraphQL.Core.Mapping;
// using CoffeeBeanery.GraphQL.Core.Sql;
//
// namespace CoffeeBeanery.GraphQL.Core.GraphQL;
//
// public sealed class GraphNodeTree
// {
//     public string Alias { get; set; }
//     public Type ModelType { get; set; }
//     public Type EntityType { get; set; }
//
//     public List<GraphEdge> Edges { get; set; }
//     public List<GraphEdge> ReverseEdges { get; set; }
//
//     public List<string> UpsertKeys { get; set; }
//
//     public NodeMap Map { get; set; }
// }
//
// public readonly record struct GraphNodeId(
//     string Alias,
//     Type? EntityType,
//     string RolePath
// );
//
// public readonly record struct FieldKey(string Alias, string Field);