namespace CoffeeBeanery.GraphQL.Core.Sql;

public class SqlQueryStructure
{
    public int Id { get; set; }
    
    public EntityNodeType EntityNodeType { get; set; } = EntityNodeType.Node;
    
    public string Query { get; set; }

    public EntityNode EntityNode { get; set; }
    
    public string Name { get; set; }
    
    public string Alias { get; set; }
    
    public string GraphQuery { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new List<string>();

    public List<string> ParentColumns { get; set; } = new List<string>();

    public List<string> SelectColumns { get; set; } = new List<string>();
        
    public bool Visited { get; set; }

    public bool HasChildren { get; set; }

    public Dictionary<string, string> ChildrenJoinColumns { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    
    public List<EntityKey> EntityKeys { get; set; } = new();

    public bool HasRequestedFields { get; set; }
    
    public string JoinOnKey { get; set; } = string.Empty;
} 