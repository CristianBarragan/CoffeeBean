using System.Reflection;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public sealed class NodeMap
    {
        public int Id { get; set; }
        
        public string Schema { get; set; } = "public";

        public bool IsRoot { get; set; }
        
        public bool IsModel { get; set; }
        
        public bool IsEntity { get; set; }

        public string Prefix { get; set; }

        public List<FieldMap> FieldMaps { get; } = new List<FieldMap>();
        public List<UpsertKey> UpsertKeys { get; private set; } = new List<UpsertKey>();
        public List<LinkKey> LinkKeys { get; private set; } = new List<LinkKey>();
        
        public List<LinkKey> ModelToEntityLinks { get; private set; } = new List<LinkKey>();

        public bool IsGraph { get; set; }
        
        public Type ModelType { get; set; }
        
        public Type EntityType { get; set; }
        
        public List<LinkKey> EntityChildren { get; set; } = new List<LinkKey>();

        public List<LinkKey> EntityParents { get; set; } = new List<LinkKey>();

        public List<LinkKey> EntityRelatedParents { get; set; } = new List<LinkKey>();
        
        public List<LinkKey> EntityRelatedChildren { get; set; } = new List<LinkKey>();
        
        public List<LinkKey> ModelChildren { get; set; } = new List<LinkKey>();
        
        public List<LinkKey> ModelParents { get; set; } = new List<LinkKey>();

        public Dictionary<string, PropertyInfo> ModelProperties { get; set; }
            = new Dictionary<string, PropertyInfo>();

        public Dictionary<string, PropertyInfo> EntityProperties { get; set; }
            = new Dictionary<string, PropertyInfo>();
        
        public Func<object, object> CreateMapper { get; set; }
        
        public Action<object, object> UpdateMapper { get; set; }

        public string Alias { get; set; }

        public string ModelName { get; set; }
        
        public GraphMap? GraphMap { get; set; }

    }

}