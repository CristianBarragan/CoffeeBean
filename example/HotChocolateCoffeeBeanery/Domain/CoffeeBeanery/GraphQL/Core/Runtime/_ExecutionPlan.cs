using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class _ExecutionPlan
{
    public Dictionary<int, ExecutionNode> Nodes { get; } = new();
    public Dictionary<int, List<ExecutionEdge>> Edges { get; } = new();
    public int RootNodeId { get; set; }
    public List<int> NodeOrder { get; } = new();
}

public sealed class ExecutionNode
{
    public int Id { get; init; }
    public string Alias { get; init; } = "";
    public int? ParentId { get; init; }
    public string? FieldName { get; init; }
    public Type? ModelType { get; set; }
    public Type? EntityType { get; set; }
    public bool IsEntity { get; set; }
    public bool IsModel { get; set; }
    public List<string> Columns { get; } = new();
    public List<(string Column, string Value)> Values { get; } = new();
}

public sealed class ExecutionEdge
{
    public int From { get; init; }
    public int To { get; init; }
    public string FieldName { get; init; } = "";
    public GraphEdgeKind Kind { get; init; }
    public string? FromColumn { get; init; }
    public string? ToColumn { get; init; }
    public List<JoinHop> Path { get; init; } = new();
}