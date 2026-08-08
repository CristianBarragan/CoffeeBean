namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public enum ModelNodeType
    {
        Node,
        Edge,
        Mutation,
        Graph
    }

    public sealed class ModelNode : ICloneable
    {
        public string Id { get; set; } = "";
        public string Prefix { get; set; } = "";

        public string Table { get; set; } = "";
        public string Column { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public string Value { get; set; } = "";

        public bool IsColumnGraph { get; set; }

        public List<ModelNodeType> ModelNodeTypes { get; set; } = new();

        public List<ModelKey> ModelChildren { get; set; } = new();

        public string RelationshipKey { get; set; } = "";

        public Dictionary<string, int> FromEnumeration { get; set; } = new();
        public Dictionary<string, int> ToEnumeration { get; set; } = new();

        public bool FromComplexModel { get; set; }

        public object Clone() => MemberwiseClone();
    }
}