namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public enum EntityNodeType
    {
        Node,
        Edge,
        Mutation,
        Graph
    }

    public sealed class EntityNode : ICloneable
    {
        public string Id { get; set; } = "";

        public string Alias { get; set; } = "";
        public string Schema { get; set; } = "";
        public string Prefix { get; set; } = "";

        public string Table { get; set; } = "";
        public string Column { get; set; } = "";
        public string SourceColumn { get; set; } = "";

        public string Value { get; set; } = "";

        public List<string> UpsertKeys { get; set; } = new();

        public List<EntityKey> EntityChildren { get; set; } = new();
        public List<EntityKey> EntityChildrenRelated { get; set; } = new();
        public List<EntityKey> ModelToEntity { get; set; } = new();

        public string EntityKey { get; set; } = "";
        public string Graph { get; set; } = "";

        public bool IsColumnGraph { get; set; }

        public List<EntityNodeType> EntityNodeTypes { get; set; } = new();

        public string RelationshipKey { get; set; } = "";

        public Dictionary<string, int> FromEnumeration { get; set; } = new();
        public Dictionary<string, int> ToEnumeration { get; set; } = new();

        public bool FromComplexModel { get; set; }

        public object Clone() => MemberwiseClone();
    }
}