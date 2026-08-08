namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public enum SqlNodeType
    {
        Node,
        Edge,
        Mutation,
        Graph
    }

    public sealed class SqlNode : ICloneable
    {
        public string Id { get; set; }
        public string Alias { get; set; }
        public string Schema { get; set; } = "";
        public string Prefix { get; set; }
        public string Table { get; set; } = "";
        public string Column { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public string Value { get; set; } = "";
        public List<string> UpsertKeys { get; set; } = new();
        public List<LinkKey> LinkKeys { get; set; } = new();
        
        public List<LinkKey> EntityChildren { get; set; } = new();
        
        public List<LinkKey> EntityParents { get; set; } = new();
        
        public List<LinkKey> EntityRelatedChildren { get; set; } = new();
        
        public List<LinkKey> EntityRelatedParents { get; set; } = new();
        
        public string Graph { get; set; } = "";
        public bool IsColumnGraph { get; set; } = false;


        public List<SqlNodeType> SqlNodeTypes { get; set; }

        // For edges
        public string RelationshipKey { get; set; } = "";

        public Dictionary<string, int> FromEnumeration { get; set; } = new();
        public Dictionary<string, int> ToEnumeration { get; set; } = new();
        
        public bool FromComplexModel { get; set; }
        
        public object Clone()
        {
            return new SqlNode()
            {
                Schema = this.Schema,
                Alias = this.Alias,
                Table = this.Table,
                Column = this.Column,
                Value = this.Value,
                UpsertKeys = this.UpsertKeys,
                LinkKeys = this.LinkKeys,
                Graph = this.Graph,
                IsColumnGraph = this.IsColumnGraph,
                SqlNodeTypes = this.SqlNodeTypes,
                RelationshipKey = this.RelationshipKey,
                FromEnumeration = this.FromEnumeration,
                ToEnumeration = this.ToEnumeration,
                EntityChildren = this.EntityChildren,
                EntityParents = this.EntityParents,
                EntityRelatedChildren = this.EntityRelatedChildren,
                EntityRelatedParents = this.EntityRelatedParents,
                SourceColumn = this.SourceColumn,
                Prefix = this.Prefix,
                Id = this.Id,
                FromComplexModel = this.FromComplexModel
            };
        }
    }
}