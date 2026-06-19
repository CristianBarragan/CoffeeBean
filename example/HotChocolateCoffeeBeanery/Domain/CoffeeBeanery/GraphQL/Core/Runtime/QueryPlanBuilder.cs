// using CoffeeBeanery.GraphQL.Core.Sql;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public static class QueryPlanBuilder
// {
//     private static readonly HashSet<string> ConnectionWrapperFields =
//         new(StringComparer.OrdinalIgnoreCase) { "nodes", "edges" };
//
//     private static readonly HashSet<string> ConnectionMetaFields =
//         new(StringComparer.OrdinalIgnoreCase) { "pageInfo", "totalCount" };
//
//     public static _ExecutionPlan Build(string rootAlias, SelectionSetNode? set)
//     {
//         var plan = new _ExecutionPlan();
//         var nextId = 0;
//
//         var resolvedRoot = ResolveRootAlias(rootAlias, set);
//
//         var root = NewNode(plan, ref nextId, resolvedRoot, null, null);
//         plan.RootNodeId = root.Id;
//
//         SeedPrimaryKey(root);
//
//         if (set != null)
//             WalkSet(plan, ref nextId, root, set);
//
//         return plan;
//     }
//
//     private static string ResolveRootAlias(string rootAlias, SelectionSetNode? set)
//     {
//         if (set is null)
//             return rootAlias;
//
//         if (NodeRegistry.FrozenEntityTrees.ContainsKey(rootAlias))
//             return rootAlias;
//
//         foreach (var s in set.Selections)
//         {
//             if (s is not FieldNode f)
//                 continue;
//
//             var name = f.Name.Value;
//
//             if (NodeRegistry.FrozenEdgeByAliasAndField.TryGetValue((rootAlias, name), out var edge))
//             {
//                 if (NodeRegistry.FrozenEntityTrees.ContainsKey(edge.ToAlias))
//                     return edge.ToAlias;
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
//     private static void SeedPrimaryKey(ExecutionNode node)
//     {
//         if (!node.Columns.Contains("Id"))
//             node.Columns.Add("Id");
//     }
//
//     private static void WalkSet(
//         _ExecutionPlan plan,
//         ref int nextId,
//         ExecutionNode current,
//         SelectionSetNode set)
//     {
//         foreach (var s in set.Selections)
//         {
//             if (s is not FieldNode f) continue;
//
//             var name = f.Name.Value;
//
//             if (ConnectionMetaFields.Contains(name))
//                 continue;
//
//             if (ConnectionWrapperFields.Contains(name))
//             {
//                 if (f.SelectionSet != null)
//                     WalkSet(plan, ref nextId, current, f.SelectionSet);
//
//                 continue;
//             }
//
//             if (string.Equals(name, NodeRegistry.ToGraphQlFieldName(current.Alias), StringComparison.OrdinalIgnoreCase))
//             {
//                 if (f.SelectionSet != null)
//                     WalkSet(plan, ref nextId, current, f.SelectionSet);
//
//                 continue;
//             }
//
//             if (NodeRegistry.FrozenEdgeByAliasAndField.TryGetValue((current.Alias, name), out var edge))
//             {
//                 var child = NewNode(plan, ref nextId, edge.ToAlias, current.Id, name);
//
//                 if (child.IsEntity)
//                     SeedPrimaryKey(child);
//
//                 plan.Edges[current.Id].Add(new ExecutionEdge
//                 {
//                     From = current.Id,
//                     To = child.Id,
//                     FieldName = name,
//                     Kind = edge.Kind,
//                     FromColumn = edge.FromColumn,
//                     ToColumn = edge.ToColumn,
//                     Path = edge.Path
//                 });
//
//                 if (f.SelectionSet != null)
//                     WalkSet(plan, ref nextId, child, f.SelectionSet);
//
//                 continue;
//             }
//
//             var leaf = NodeRegistry.ResolveLeaf(current.Alias, name);
//
//             if (leaf.Count > 0)
//             {
//                 foreach (var (ea, col) in leaf)
//                 {
//                     if (!current.Columns.Contains(col))
//                         current.Columns.Add(col);
//                 }
//
//                 if (f.SelectionSet != null)
//                     WalkSet(plan, ref nextId, current, f.SelectionSet);
//
//                 continue;
//             }
//
//             if (f.SelectionSet != null)
//             {
//                 WalkSet(plan, ref nextId, current, f.SelectionSet);
//                 continue;
//             }
//         }
//     }
// }