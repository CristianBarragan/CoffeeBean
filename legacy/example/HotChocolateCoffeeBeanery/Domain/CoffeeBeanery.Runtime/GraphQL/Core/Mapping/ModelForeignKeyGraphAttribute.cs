namespace CoffeeBeanery.GraphQL.Core.Mapping;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModelForeignKeyGraphAttribute : Attribute
{
    public string Edges { get; }

    public ModelForeignKeyGraphAttribute(string edges)
    {
        Edges = edges;
    }
}