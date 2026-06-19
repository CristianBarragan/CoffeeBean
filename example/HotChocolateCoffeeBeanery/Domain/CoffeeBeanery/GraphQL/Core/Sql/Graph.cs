namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class Graph
{
    public Dictionary<string, GraphNode> Nodes { get; } = new();

    public List<GraphEdge> Edges { get; } = new();
}