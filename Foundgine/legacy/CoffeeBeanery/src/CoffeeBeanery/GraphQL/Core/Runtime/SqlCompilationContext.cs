using System.Collections.Generic;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public sealed class SqlCompilationContext
    {
        public string SelectSql { get; set; } = "";
        public string UpsertSql { get; set; } = "";

        public bool HasPagination { get; set; }
        
        public bool HasTotalCount { get; set; }

        public Dictionary<string, string> SqlWhereStatement { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        public Dictionary<string, string> SqlOrderStatements { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Pagination Pagination { get; set; } = new Pagination();
        
        public Dictionary<string, SqlNode> SqlNodesApplied { get; set; }
        
        public Dictionary<string, Type> SplitOnDapper { get; set; } = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

        public Dictionary<string, NodeTree> EntityTrees { get; set; } = new Dictionary<string, NodeTree>(StringComparer.InvariantCultureIgnoreCase);
        
        public Dictionary<string, NodeTree> ModelTrees { get; set; } = new Dictionary<string, NodeTree>(StringComparer.InvariantCultureIgnoreCase);
        
        public NodeTree RelativeTree { get; set; }
    }
}