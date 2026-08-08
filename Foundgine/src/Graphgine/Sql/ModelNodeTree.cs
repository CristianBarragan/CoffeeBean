namespace Graphgine.Sql;

public class ModelNodeTree
{
    public string Alias { get; set; }
    public string ModelName { get; set; }
    public Type ModelType { get; set; }
    public Type? EntityType { get; set; }
}