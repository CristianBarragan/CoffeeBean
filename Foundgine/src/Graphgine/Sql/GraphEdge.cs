using Graphgine.Sql;

public sealed class GraphEdge
{
    public string FromAlias { get; set; }
    public string ToAlias { get; set; }
    public string FieldName { get; set; }
    public GraphEdgeKind Kind { get; set; }
    public string FromColumn { get; set; }
    public string ToColumn { get; set; }

    public List<JoinHop> Path { get; set; } = new();
}

public enum GraphNodeKind
{
    Model,
    Entity,
    Aggregate
}

public enum GraphEdgeKind
{
    ModelNavigation,
    EntityNavigation,
    ModelToEntity,
    ScalarField
}