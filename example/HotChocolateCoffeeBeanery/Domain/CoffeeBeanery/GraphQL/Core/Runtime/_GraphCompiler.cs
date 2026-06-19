using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class _GraphCompiler
{
    public static _ExecutionPlan Compile(Graph graph)
    {
        var plan = new _ExecutionPlan();

        var nodeMap = new Dictionary<string, int>();
        var nextId = 1;

        foreach (var kv in graph.Nodes)
        {
            var node = kv.Value;

            var id = nextId++;
            nodeMap[node.Alias] = id;

            plan.Nodes[id] = new ExecutionNode
            {
                Id = id,
                Alias = node.Alias,
                ModelType = node.ModelType,
                EntityType = node.EntityType,
                IsModel = node.IsModel,
                IsEntity = node.IsEntity
            };
        }

        plan.RootNodeId = nodeMap.First().Value;

        foreach (var kv in graph.Nodes)
        {
            var node = kv.Value;
            var fromId = nodeMap[node.Alias];

            foreach (var edge in node.Edges)
            {
                var toId = nodeMap[edge.ToAlias];

                if (!plan.Edges.TryGetValue(fromId, out var list))
                {
                    list = new List<ExecutionEdge>();
                    plan.Edges[fromId] = list;
                }

                list.Add(new ExecutionEdge
                {
                    From = fromId,
                    To = toId,
                    FieldName = edge.FieldName,
                    Kind = edge.Kind,
                    FromColumn = edge.FromColumn,
                    ToColumn = edge.ToColumn
                });
            }
        }

        return plan;
    }
}