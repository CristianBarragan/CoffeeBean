namespace CoffeeBeanery.GraphQL.Core.Sql;

public class GraphModel
{
    public Guid? EdgeKey { get; set; }
    
    public EdgeRelationshipDirection? RelationshipDirection { get; set; }
    
    public Status? Status { get; set; }
    
    public DateTime? CreatedAfter { get; set; }
    
    public int? MinDepth { get; set; }
    
    public int? MaxDepth { get; set; }
    
    public bool? Recursive { get; set; }
}

public enum Status
{
    Inactive,
    Active
}

public enum EdgeRelationshipDirection
{
    Outgoing,
    Incoming,
    Undirected
}

public interface IModel
{
    
}