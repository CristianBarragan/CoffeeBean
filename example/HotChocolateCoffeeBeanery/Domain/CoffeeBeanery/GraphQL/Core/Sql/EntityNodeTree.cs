using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public sealed class EntityNodeTree
    {
        
        public string Alias { get; set; } = "";
        public string ModelName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Schema { get; set; } = "public";
        public int Id { get; init; }

        public string Prefix { get; set; }

        public NodeMap NodeMap { get; set; }
        
        public List<EntityKey> EntityChildren { get; set; } = new();
        
        public List<EntityKey> EntityChildrenRelated { get; set; } = new();
        
        public List<EntityKey> ModelToEntity { get; set; } = new();

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