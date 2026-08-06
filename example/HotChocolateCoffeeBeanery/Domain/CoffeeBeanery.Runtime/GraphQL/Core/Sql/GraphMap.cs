namespace CoffeeBeanery.GraphQL.Core.Sql;

public class GraphMap
{
    public string GraphName     { get; set; }
    public string EdgeLabel     { get; set; }
    
    public string EdgeKeyColumn { get; set; }
    public GraphVertex FromVertex { get; set; }
    public GraphVertex ToVertex   { get; set; }
    public string FromJoinColumn { get; set; }
    public string ToJoinColumn { get; set; }
}

public class GraphVertex
{
    public string Label     { get; set; }
    public string KeyColumn { get; set; }
    public string AliasTo   { get; set; }
    public string KeyType { get; set; } = "UUID";
}