using System.Collections.Frozen;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.Service.Materialization
{
    public static class DynamicGraphMaterializer
    {
        public static List<M> Materialize<M>(
            _ExecutionPlan plan,
            IReadOnlyList<int> nodeIdOrder,
            IReadOnlyList<object?[]> rowMatrix)
            where M : class
        {
            if (nodeIdOrder.Count == 0 || rowMatrix.Count == 0)
                return new List<M>();

            if (!nodeIdOrder.Contains(plan.RootNodeId))
                throw new InvalidOperationException(
                    $"RootNodeId '{plan.RootNodeId}' is not present in nodeIdOrder [{string.Join(", ", nodeIdOrder)}]. " +
                    "The root node must always be requested/selected (QueryPlanBuilder seeds its PK automatically).");

            NodeRegistry.Freeze();

            var modelTrees = NodeRegistry.FrozenModelTrees;
            var entityTrees = NodeRegistry.FrozenEntityTrees;
            // var factories = NodeRegistry.FrozenModelFactories;
            // var appliers = NodeRegistry.FrozenEntityToModelAppliers;
            // var keyGetters = NodeRegistry.FrozenKeyGetters;
            // var attachers = NodeRegistry.FrozenAttachers;

            var trees = nodeIdOrder.ToDictionary(id => id, id => ResolveTree(plan.Nodes[id].Alias, modelTrees, entityTrees));

            var childNodeIdsByParent = new Dictionary<int, List<int>>();
            foreach (var nodeId in nodeIdOrder)
            {
                if (!plan.Edges.TryGetValue(nodeId, out var edges)) continue;
                childNodeIdsByParent[nodeId] = edges
                    .Where(e => nodeIdOrder.Contains(e.To))
                    .Select(e => e.To)
                    .ToList();
            }

            var modelCache = new Dictionary<int, Dictionary<string, object>>();
            var modelOrder = new Dictionary<int, List<string>>();
            var attached = new Dictionary<(object Parent, int ChildNodeId), HashSet<string>>();

            foreach (var nodeId in nodeIdOrder)
            {
                modelCache[nodeId] = new Dictionary<string, object>(StringComparer.Ordinal);
                modelOrder[nodeId] = new List<string>();
            }

            foreach (var row in rowMatrix)
            {
                var rowModels = new Dictionary<int, (object Model, string Key)>();

                for (var i = 0; i < nodeIdOrder.Count && i < row.Length; i++)
                {
                    var nodeId = nodeIdOrder[i];
                    var entityInstance = row[i];
                    if (entityInstance is null) continue;

                    // if (!keyGetters.TryGetValue(plan.Nodes[nodeId].Alias, out var keyGetter))
                    //     continue;

                    // var pk = keyGetter(entityInstance);
                    // if (pk is null) continue;
                    //
                    // if (!modelCache[nodeId].TryGetValue(pk, out var modelInstance))
                    // {
                    //     // modelInstance = CreateModelFromEntity(plan.Nodes[nodeId].Alias, entityInstance, factories, appliers);
                    //     modelCache[nodeId][pk] = modelInstance;
                    //     modelOrder[nodeId].Add(pk);
                    // }
                    //
                    // rowModels[nodeId] = (modelInstance, pk);
                }

                foreach (var nodeId in nodeIdOrder)
                {
                    if (!rowModels.TryGetValue(nodeId, out var parent)) continue;
                    if (!childNodeIdsByParent.TryGetValue(nodeId, out var childIds)) continue;

                    foreach (var childNodeId in childIds)
                    {
                        if (!rowModels.TryGetValue(childNodeId, out var child))
                            continue;

                        // if (!attachers.TryGetValue((plan.Nodes[nodeId].Alias, plan.Nodes[childNodeId].Alias), out var attach))
                        //     continue;
                        //
                        // var trackKey = (parent.Model, childNodeId);
                        // if (!attached.TryGetValue(trackKey, out var seen))
                        //     attached[trackKey] = seen = new HashSet<string>(StringComparer.Ordinal);
                        //
                        // if (seen.Add(child.Key))
                        //     attach(parent.Model, child.Model);
                    }
                }
            }

            return modelOrder[plan.RootNodeId].Select(k => (M)modelCache[plan.RootNodeId][k]).ToList();
        }

        private sealed record Tree(
            string Alias,
            Type ModelType,
            Type? EntityType);

        private static Tree ResolveTree(
            string alias,
            FrozenDictionary<string, ModelNodeTree> modelTrees,
            FrozenDictionary<string, EntityNodeTree> entityTrees)
        {
            if (entityTrees.TryGetValue(alias, out var et))
                return new Tree(alias, et.ModelType, et.EntityType);

            if (modelTrees.TryGetValue(alias, out var mt))
                return new Tree(alias, mt.ModelType, mt.EntityType);

            throw new InvalidOperationException(
                $"No EntityNodeTree or ModelNodeTree registered for alias '{alias}'. " +
                "Make sure its MappingSet.Register(...) ran before this query executed.");
        }

        private static object CreateModelFromEntity(
            string alias,
            object entityInstance,
            FrozenDictionary<string, Func<object>> factories,
            FrozenDictionary<string, Action<object, object>> appliers)
        {
            if (!factories.TryGetValue(alias, out var factory))
                throw new InvalidOperationException(
                    $"No compiled model factory registered for alias '{alias}'. " +
                    "Make sure its mapping class's generated Register() ran (NodeTreeEmitter.EmitModelFactory).");

            var model = factory();

            if (appliers.TryGetValue(alias, out var apply))
                apply(entityInstance, model);

            return model;
        }
    }
}