using System.Collections.Generic;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Helper;

namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public static class SqlNodeRegistry
    {
        public static Dictionary<string, SqlNode> ModelNodes { get; } = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
        
        public static Dictionary<string, SqlNode> EntityNodes { get; } = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
        
        public static Dictionary<string, NodeTree> ModelTrees { get; } = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
        
        public static Dictionary<string, NodeTree> EntityTrees { get; } = new Dictionary<string, NodeTree>(StringComparer.OrdinalIgnoreCase);
        
        public static List<string> EntityNames { get; } = new List<string>();
        
        public static List<string> ModelNames { get; } = new List<string>();
        
        public static List<Type> EntityTypes { get; } = new List<Type>();
        
        public static List<Type> ModelTypes { get; } = new List<Type>();

        public static void RegisterNode(string modelKey, string entityKey, SqlNode node, Type modelType, Type entityType, bool isEntity)
        {
            if (!ModelNames.Contains(modelType.Name))
            {
                ModelNames.Add(modelType.Name);
            }
            
            if (!EntityNames.Contains(entityType.Name) && isEntity)
            {
                EntityNames.Add(entityType.Name);
            }
            
            if (!ModelTypes.Contains(modelType))
            {
                ModelTypes.Add(modelType);
            }
            
            if (!EntityTypes.Contains(entityType) && isEntity)
            {
                EntityTypes.Add(entityType);    
            }
            
            if (!EntityNodes.ContainsKey(entityKey))
            {
                EntityNodes[entityKey] = node;
            }
            
            if (!ModelNodes.ContainsKey(modelKey))
            {
                ModelNodes[modelKey] = node;
            }
        }
    }
}
