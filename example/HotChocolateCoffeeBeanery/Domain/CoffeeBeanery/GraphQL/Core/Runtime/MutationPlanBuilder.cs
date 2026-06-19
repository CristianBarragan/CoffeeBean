// using CoffeeBeanery.GraphQL.Core.Sql;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public static class MutationPlanBuilder
// {
//     public static _ExecutionPlan Build(string rootAlias, IValueNode node)
//     {
//         var plan = new _ExecutionPlan();
//         var nextId = 0;
//
//         var resolvedRootAlias = ResolveRootAlias(rootAlias, node);
//         var root = NewNode(
//             plan,
//             ref nextId,
//             resolvedRootAlias,
//             parentId: null,
//             fieldName: null);
//
//         plan.RootNodeId = root.Id;
//         WalkNode(plan, ref nextId, root, node);
//
//         return plan;
//     }
//
//     private static string ResolveRootAlias(string rootAlias, IValueNode node)
//     {
//         if (node is not ObjectValueNode obj)
//             return rootAlias;
//
//         if (NodeRegistry.FrozenEntityTrees.ContainsKey(rootAlias))
//             return rootAlias;
//
//         foreach (var f in obj.Fields)
//         {
//             var name = f.Name.Value;
//
//             Console.WriteLine($"[WALK] current.Alias={rootAlias}, field={name}, valueType={f.Value.GetType().Name}");
//             
//             if (NodeRegistry.FrozenEdgeByAliasAndField.TryGetValue((rootAlias, name), out var edge))
//             {
//                 return edge.ToAlias;
//             }
//         }
//
//         return rootAlias;
//     }
//
//     private static ExecutionNode NewNode(
//         _ExecutionPlan plan,
//         ref int nextId,
//         string alias,
//         int? parentId,
//         string? fieldName)
//     {
//         var node = new ExecutionNode
//         {
//             Id = nextId++,
//             Alias = alias,
//             ParentId = parentId,
//             FieldName = fieldName,
//             IsEntity = NodeRegistry.FrozenEntityTrees.ContainsKey(alias)
//         };
//
//         plan.Nodes[node.Id] = node;
//         plan.NodeOrder.Add(node.Id);
//
//         if (parentId is { } pid && !plan.Edges.ContainsKey(pid))
//             plan.Edges[pid] = new List<ExecutionEdge>();
//
//         return node;
//     }
//
//     private static void WalkNode(
//         _ExecutionPlan plan,
//         ref int nextId,
//         ExecutionNode current,
//         IValueNode node)
//     {
//         if (node is not ObjectValueNode obj)
//             return;
//
//         foreach (var f in obj.Fields)
//         {
//             var name = f.Name.Value;
//
//             if (string.Equals(name, NodeRegistry.ToGraphQlFieldName(current.Alias), StringComparison.OrdinalIgnoreCase))
//             {
//                 if (f.Value is ObjectValueNode)
//                     WalkNode(plan, ref nextId, current, f.Value);
//
//                 continue;
//             }
//
//             if (f.Value is ObjectValueNode or ListValueNode)
//             {
//                 if (NodeRegistry.FrozenEdgeByAliasAndField.TryGetValue((current.Alias, name), out var edge))
//                 {
//                     var child = NewNode(plan, ref nextId, edge.ToAlias, current.Id, name);
//
//                     plan.Edges[current.Id].Add(new ExecutionEdge
//                     {
//                         From = current.Id,
//                         To = child.Id,
//                         FieldName = name,
//                         Kind = edge.Kind,
//                         FromColumn = edge.FromColumn,
//                         ToColumn = edge.ToColumn,
//                         Path = edge.Path
//                     });
//
//                     WalkNode(plan, ref nextId, child, f.Value);
//                     continue;
//                 }
//
//                 if (f.Value is ObjectValueNode)
//                 {
//                     WalkNode(plan, ref nextId, current, f.Value);
//                     continue;
//                 }
//
//                 if (f.Value is ListValueNode listNode)
//                 {
//                     foreach (var item in listNode.Items)
//                         WalkNode(plan, ref nextId, current, item);
//
//                     continue;
//                 }
//
//                 continue;
//             }
//
//             var raw = f.Value.Value?.ToString();
//             if (raw == null) continue;
//
//             foreach (var (_, col) in NodeRegistry.ResolveLeaf(current.Alias, name))
//                 SetValue(current, col, raw);
//         }
//     }
//
//     private static void SetValue(ExecutionNode node, string column, string value)
//     {
//         for (int i = 0; i < node.Values.Count; i++)
//         {
//             if (node.Values[i].Column == column)
//             {
//                 node.Values[i] = (column, value);
//                 return;
//             }
//         }
//
//         node.Values.Add((column, value));
//     }
// }