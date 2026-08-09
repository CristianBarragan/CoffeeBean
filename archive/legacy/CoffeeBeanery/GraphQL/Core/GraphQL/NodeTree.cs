using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.GraphQL
{
    public sealed class NodeTree
    {
        
        public string Alias { get; set; } = "";
        public string ModelName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Schema { get; set; } = "public";
        public int Id { get; init; }

        public string Prefix { get; set; }

        public NodeMap NodeMap { get; set; }

        public List<LinkKey> Parents { get; set; } = new();
        
        public List<LinkKey> RelatedParents { get; set; } = new();
        
        public List<LinkKey> RelatedChildren { get; set; } = new();
        
        public List<LinkKey> Children { get; set; } = new();

        public List<LinkKey> ModelToEntityLinks { get; set; } = new();
        
        public List<LinkKey> ModelChildren { get; set; } = new();
        
        public List<LinkKey> ModelParents { get; set; } = new();

        public List<FieldMap> Mapping { get; set; } = new();
        
        public List<string> UpsertKeys { get; set; } = new();

        public Type EntityType { get; set; }
        
        public Type ModelType { get; set; }

        public bool IsGraph { get; set; }

        public bool IsEntity { get; set; }

        public bool IsModel { get; set; }
        
        public GraphMap? GraphMap { get; set; }
        
    }
}