// using System.Collections;
// using System.Reflection;
// using CoffeeBeanery.CQRS;
// using CoffeeBeanery.GraphQL.Core.GraphQL;
// using CoffeeBeanery.GraphQL.Core.Runtime;
// using CoffeeBeanery.GraphQL.Core.Sql;
// using CoffeeBeanery.GraphQL.Helper;
// using Npgsql;
// using CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree = CoffeeBeanery.GraphQL.Core.GraphQL.CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree;
// using ModelNodeTree = CoffeeBeanery.GraphQL.Core.GraphQL.ModelNodeTree;
//
// namespace CoffeeBeanery.Service
// {
//     public class QueryHandler<M> : ProcessQuery<M>,
//         IQuery<ProcessQueryParameters,
//             (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
//         where M : class
//     {
//         private readonly IMapper _mapper;
//
//         public QueryHandler(
//             ILoggerFactory loggerFactory,
//             NpgsqlDataSource dataSource,
//             IMapper mapper)
//             : base(loggerFactory, dataSource)
//         {
//             _mapper = mapper;
//         }
//
//         public override (List<M> models,
//                          int? startCursor,
//                          int? endCursor,
//                          int? totalCount,
//                          int? totalPageRecords)
//         MappingConfiguration(
//             SqlCompilationContext context,
//             List<string> aliasOrder,
//             List<object?[]> rowMatrix,
//             List<Type> types)
//         {
//             var modelTrees  = context.ModelTrees;
//             var entityTrees = context.EntityTrees;
//
//             int totalCount  = 0;
//             int pageRecords = 0;
//
//             var rootModelTree  = GetRootFromWrapper<M>(modelTrees);
//             var rootAlias      = rootModelTree.Alias;
//             var rootAliasIndex = aliasOrder.FindIndex(a => a.Equals(rootAlias, StringComparison.OrdinalIgnoreCase));
//
//             if (rootAliasIndex < 0)
//                 throw new InvalidOperationException(
//                     $"Root alias '{rootAlias}' (resolved from {typeof(M).Name}'s List<T> property) " +
//                     $"is not present in this query's alias order [{string.Join(", ", aliasOrder)}].");
//
//             // Group rows by the root's actual upsert key (whatever entity property that is for
//             // this mapping) instead of guessing from a "*Key"-suffixed Guid property - a row whose
//             // key doesn't happen to match that naming convention used to be silently dropped.
//             var rowsByRootKey = new Dictionary<string, List<object?[]>>(StringComparer.Ordinal);
//             var rootKeyOrder  = new List<string>();
//
//             foreach (var row in rowMatrix)
//             {
//                 if (row[rootAliasIndex] is TotalPageRecords tp) { pageRecords = tp.PageRecords; continue; }
//                 if (row[rootAliasIndex] is TotalRecordCount tr) { totalCount  = tr.RecordCount;  continue; }
//
//                 var rootEntity = row[rootAliasIndex];
//                 if (rootEntity is null) continue;
//
//                 var rootKey = ResolveUpsertKeyValue(rootAlias, rootEntity, entityTrees, modelTrees);
//                 if (rootKey is null) continue; // no key resolvable for this alias - can't group safely
//
//                 if (!rowsByRootKey.TryGetValue(rootKey, out var bucket))
//                 {
//                     bucket = new List<object?[]>();
//                     rowsByRootKey[rootKey] = bucket;
//                     rootKeyOrder.Add(rootKey);
//                 }
//
//                 bucket.Add(row);
//             }
//
//             var wrappers = new List<M>();
//
//             foreach (var rootKey in rootKeyOrder)
//             {
//                 var groupRows  = rowsByRootKey[rootKey];
//                 var rawByAlias = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
//
//                 // Persists across every row in this root's group - disambiguates repeated entity
//                 // types appearing at indices with no direct alias match. Must NOT be reset per-row
//                 // (resetting here used to defeat its own purpose).
//                 var seenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
//
//                 foreach (var row in groupRows)
//                 {
//                     for (var i = 0; i < row.Length && i < aliasOrder.Count; i++)
//                     {
//                         if (row[i] is null) continue;
//                         if (row[i] is TotalPageRecords || row[i] is TotalRecordCount) continue;
//
//                         var alias = aliasOrder[i];
//
//                         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree? entityTree;
//
//                         if (entityTrees.TryGetValue(alias, out var byAlias))
//                         {
//                             entityTree = byAlias;
//                         }
//                         else
//                         {
//                             var entityType = row[i]!.GetType();
//                             var seen = seenCounts.GetValueOrDefault(entityType.Name, 0);
//                             seenCounts[entityType.Name] = seen + 1;
//                             entityTree = ResolveEntityTree(alias, entityType, types.ElementAtOrDefault(i), seen, entityTrees);
//                         }
//
//                         if (entityTree is null) continue;
//
//                         if (!rawByAlias.TryGetValue(entityTree.Alias, out var rawBucket))
//                         {
//                             rawBucket = new List<object>();
//                             rawByAlias[entityTree.Alias] = rawBucket;
//                         }
//
//                         rawBucket.Add(row[i]!);
//                     }
//                 }
//
//                 var dedupedRaw = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
//                 foreach (var (alias, bucket) in rawByAlias)
//                     dedupedRaw[alias] = DeduplicateByKey(alias, bucket, entityTrees, modelTrees);
//
//                 var rootObject = Activator.CreateInstance<M>()!;
//
//                 var nodeResult = new NodeResult
//                 {
//                     RootObject    = rootObject,
//                     CurrentObject = rootObject,
//                     ModelTypes    = context.SplitOnDapper.Select(a => a.Value).ToList(),
//                     RawObjects    = dedupedRaw,
//                     Objects       = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
//                     AllObjects    = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase),
//                 };
//
//                 Build(rootModelTree, modelTrees, entityTrees, nodeResult, visiting: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
//
//                 var wrapperObj = (M)nodeResult.RootObject;
//
//                 if (nodeResult.Objects.TryGetValue(rootModelTree.Alias, out var rootObj))
//                     Attach(wrapperObj, rootObj, nodeResult, entityTrees, modelTrees, childAlias: rootModelTree.Alias);
//
//                 wrappers.Add(wrapperObj);
//             }
//
//             return (
//                 wrappers,
//                 context.Pagination?.StartCursor,
//                 context.Pagination?.EndCursor,
//                 totalCount,
//                 pageRecords);
//         }
//
//         // ---------------------------------------------------------------- alias -> model
//
//         private List<object> MapAlias(string alias, NodeResult nodeResult)
//         {
//             if (!nodeResult.RawObjects.TryGetValue(alias, out var rawList) || rawList.Count == 0)
//                 return new List<object>();
//
//             var mapped = new List<object>(rawList.Count);
//
//             foreach (var raw in rawList)
//             {
//                 try
//                 {
//                     mapped.Add(_mapper.MapByAlias(raw.GetType(), raw, alias));
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.ForegroundColor = ConsoleColor.Yellow;
//                     Console.WriteLine($"[WARN] MapByAlias failed for '{alias}': {ex.Message}");
//                     Console.ResetColor();
//                 }
//             }
//
//             return mapped;
//         }
//
//         // ---------------------------------------------------------------- recursive build
//
//         private void Build(
//             ModelNodeTree modelTree,
//             Dictionary<string, ModelNodeTree> modelTrees,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             NodeResult nodeResult,
//             HashSet<string> visiting)
//         {
//             // Cycle guard - CustomerCustomerEdge's Customer<->Customer shape (and anything similar)
//             // would otherwise recurse forever the moment a child alias loops back to an ancestor.
//             if (!visiting.Add(modelTree.Alias))
//                 return;
//
//             try
//             {
//                 if (!nodeResult.Objects.TryGetValue(modelTree.Alias, out var currentModel))
//                 {
//                     var mappedList = MapAlias(modelTree.Alias, nodeResult);
//
//                     if (mappedList.Count > 0)
//                     {
//                         currentModel = mappedList[0];
//                         nodeResult.Objects[modelTree.Alias]    = currentModel;
//                         nodeResult.AllObjects[modelTree.Alias] = mappedList;
//                     }
//                     else
//                     {
//                         currentModel = Activator.CreateInstance(modelTree.ModelType)!;
//                         nodeResult.Objects[modelTree.Alias] = currentModel;
//                     }
//                 }
//
//                 nodeResult.CurrentObject = currentModel!;
//
//                 foreach (var linkKey in modelTree.ModelChildren ?? Enumerable.Empty<ModelKey>())
//                 {
//                     // ModelKey.To is a Model *type name* (see NodeBuilder.InferModelChildren), not
//                     // necessarily an alias - only resolve by alias as a secondary guess.
//                     var childModelTree = ResolveModelTreeByNameOrAlias(linkKey.To, modelTrees);
//                     if (childModelTree is null) continue;
//
//                     Build(childModelTree, modelTrees, entityTrees, nodeResult, visiting);
//
//                     if (nodeResult.Objects.TryGetValue(childModelTree.Alias, out var childObj))
//                         Attach(currentModel, childObj, nodeResult, entityTrees, modelTrees,
//                             prefix: childModelTree.Prefix, childAlias: childModelTree.Alias);
//                 }
//
//                 if (!modelTree.IsEntity) return;
//
//                 var entityTree = entityTrees.TryGetValue(modelTree.Alias, out var et)
//                     ? et
//                     : entityTrees.Values.FirstOrDefault(t => t.ModelType == modelTree.ModelType && t.IsEntity);
//
//                 if (entityTree is null) return;
//
//                 foreach (var childLink in entityTree.EntityChildren.Concat(entityTree.EntityChildrenRelated))
//                 {
//                     var childAlias = !string.IsNullOrWhiteSpace(childLink.AliasTo) ? childLink.AliasTo : childLink.To;
//
//                     // Only follow links into aliases this query actually fetched - entityTrees may
//                     // contain registrations for mappings outside this particular query's row matrix.
//                     if (!nodeResult.RawObjects.ContainsKey(childAlias) && !modelTrees.ContainsKey(childAlias))
//                         continue;
//
//                     var childEntTree = ResolveChildEntityTree(childAlias, entityTrees);
//                     if (childEntTree is null) continue;
//
//                     var childModelTree = ResolveModelTreeForEntity(childEntTree, childAlias, modelTrees);
//                     if (childModelTree is null) continue;
//
//                     Build(childModelTree, modelTrees, entityTrees, nodeResult, visiting);
//
//                     if (nodeResult.Objects.TryGetValue(childModelTree.Alias, out var childObj))
//                         Attach(currentModel, childObj, nodeResult, entityTrees, modelTrees,
//                             prefix: childModelTree.Prefix, childAlias: childModelTree.Alias);
//                 }
//
//                 ProjectAggregates(nodeResult, modelTrees);
//             }
//             finally
//             {
//                 visiting.Remove(modelTree.Alias);
//             }
//         }
//
//         // ---------------------------------------------------------------- dedup (generic key)
//
//         // Reads the alias's declared UpsertKeys ("Entity~KeyProperty") instead of guessing from a
//         // "*Key"-suffixed Guid property - works for any key shape (Guid, int, string, composite-via-
//         // first-segment), and no longer silently drops rows whose key doesn't fit that convention.
//         private static string? ResolveUpsertKeyValue(
//             string alias, object instance,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, ModelNodeTree> modelTrees)
//         {
//             List<string>? upsertKeys =
//                 entityTrees.TryGetValue(alias, out var et) ? et.UpsertKeys :
//                 modelTrees.TryGetValue(alias, out var mt) ? mt.UpsertKeys :
//                 null;
//
//             var spec = upsertKeys?.FirstOrDefault();
//             if (spec is null) return null;
//
//             var parts = spec.Split('~');
//             var keyPropName = parts.Length == 2 ? parts[1] : spec;
//
//             var prop = instance.GetType().GetProperty(keyPropName, BindingFlags.Public | BindingFlags.Instance);
//             var value = prop?.GetValue(instance);
//
//             return value?.ToString();
//         }
//
//         private static List<object> DeduplicateByKey(
//             string alias, List<object> items,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, ModelNodeTree> modelTrees)
//         {
//             if (items.Count == 0) return items;
//
//             var seen   = new HashSet<string>(StringComparer.Ordinal);
//             var result = new List<object>(items.Count);
//
//             foreach (var item in items)
//             {
//                 var key = ResolveUpsertKeyValue(alias, item, entityTrees, modelTrees);
//                 if (key is null) { result.Add(item); continue; } // no key metadata - can't dedupe, keep as-is
//
//                 if (seen.Add(key))
//                     result.Add(item);
//             }
//
//             return result;
//         }
//
//         // ---------------------------------------------------------------- aggregate projection
//
//         private static void ProjectAggregates(
//             NodeResult nodeResult,
//             Dictionary<string, ModelNodeTree> modelTrees)
//         {
//             foreach (var modelTree in modelTrees.Values.Where(t => t.IsModel))
//             {
//                 if (!nodeResult.Objects.TryGetValue(modelTree.Alias, out var targetModel)) continue;
//
//                 var targetType = targetModel.GetType();
//
//                 foreach (var field in modelTree.Mapping)
//                 {
//                     var sourceAlias = field.DestinationAlias;
//                     if (string.IsNullOrWhiteSpace(sourceAlias)) continue;
//                     if (!nodeResult.Objects.TryGetValue(sourceAlias, out var sourceObj)) continue;
//
//                     var sourceProp = sourceObj.GetType().GetProperty(field.DestinationName.Split('.').Last());
//                     var targetProp = targetType.GetProperty(field.SourceName);
//
//                     if (sourceProp is null || targetProp is null) continue;
//
//                     SafeSet(targetProp, targetModel, sourceProp.GetValue(sourceObj));
//                 }
//             }
//         }
//
//         private static void SafeSet(PropertyInfo prop, object instance, object? value)
//         {
//             if (value is null) return;
//             var targetType = prop.PropertyType;
//
//             if (targetType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(targetType))
//             {
//                 var elementType = targetType.GetGenericArguments()[0];
//                 var existing    = prop.GetValue(instance);
//                 if (existing is null)
//                 {
//                     existing = Activator.CreateInstance(targetType);
//                     prop.SetValue(instance, existing);
//                 }
//                 var list = (IList)existing!;
//
//                 if (value is IEnumerable enumerable && value is not string)
//                 {
//                     foreach (var v in enumerable)
//                         if (v is not null && elementType.IsAssignableFrom(v.GetType()) && !list.Contains(v))
//                             list.Add(v);
//                 }
//                 else if (elementType.IsAssignableFrom(value.GetType()) && !list.Contains(value))
//                 {
//                     list.Add(value);
//                 }
//                 return;
//             }
//
//             if (targetType.IsAssignableFrom(value.GetType()))
//             {
//                 prop.SetValue(instance, value);
//                 return;
//             }
//
//             // Generic fallback for "target wants a scalar, source is a whole object" cases (e.g.
//             // a model's CustomerKey field sourced from a nested Customer object): look for a
//             // same-named property on the source object rather than a hardcoded property name.
//             var matchingSourceProp = value.GetType().GetProperty(prop.Name);
//             if (matchingSourceProp is not null && targetType.IsAssignableFrom(matchingSourceProp.PropertyType))
//                 prop.SetValue(instance, matchingSourceProp.GetValue(value));
//         }
//
//         // ---------------------------------------------------------------- attach / nesting
//
//         private static void Attach(
//             object? parent, object? child, NodeResult nodeResult,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, ModelNodeTree> modelTrees,
//             string prefix = "", string childAlias = "")
//         {
//             if (parent is null || child is null) return;
//
//             var childType = child.GetType();
//             var prop = ResolveNavigationProperty(parent.GetType(), childType, prefix, childAlias);
//             if (prop is null) return;
//
//             if (typeof(IList).IsAssignableFrom(prop.PropertyType))
//             {
//                 var list = prop.GetValue(parent) as IList;
//                 if (list is null)
//                 {
//                     list = (IList)Activator.CreateInstance(prop.PropertyType)!;
//                     prop.SetValue(parent, list);
//                 }
//
//                 IEnumerable<object> candidates =
//                     !string.IsNullOrWhiteSpace(childAlias) && nodeResult.AllObjects.TryGetValue(childAlias, out var bucket)
//                         ? bucket
//                         : new[] { child };
//
//                 foreach (var item in candidates)
//                 {
//                     if (item is null) continue;
//
//                     var newKey = !string.IsNullOrWhiteSpace(childAlias)
//                         ? ResolveUpsertKeyValue(childAlias, item, entityTrees, modelTrees)
//                         : null;
//
//                     if (newKey is not null)
//                     {
//                         var foundIndex = -1;
//                         for (var j = 0; j < list.Count; j++)
//                         {
//                             var existingKey = ResolveUpsertKeyValue(childAlias, list[j]!, entityTrees, modelTrees);
//                             if (existingKey == newKey) { foundIndex = j; break; }
//                         }
//
//                         if (foundIndex >= 0) { list[foundIndex] = item; continue; }
//                     }
//
//                     if (!list.Contains(item)) list.Add(item);
//                 }
//             }
//             else if (prop.GetValue(parent) is null)
//             {
//                 prop.SetValue(parent, child);
//             }
//
//             if (!string.IsNullOrWhiteSpace(childAlias))
//                 nodeResult.Objects[childAlias] = child;
//         }
//
//         private static PropertyInfo? ResolveNavigationProperty(
//             Type parentType, Type childType, string prefix, string childAlias)
//         {
//             if (!string.IsNullOrWhiteSpace(childAlias))
//             {
//                 var aliasProp = parentType.GetProperty(
//                     childAlias, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
//                 if (aliasProp is not null) return aliasProp;
//             }
//
//             return parentType
//                 .GetProperties(BindingFlags.Public | BindingFlags.Instance)
//                 .OrderByDescending(a =>
//                     !string.IsNullOrEmpty(prefix) && a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
//                 .FirstOrDefault(p =>
//                 {
//                     if (p.PropertyType == childType) return true;
//                     if (!p.PropertyType.IsGenericType) return false;
//                     return p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) &&
//                            p.PropertyType.GetGenericArguments()[0] == childType;
//                 });
//         }
//
//         // ---------------------------------------------------------------- tree resolution helpers
//
//         private static CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree? ResolveChildEntityTree(
//             string childAlias, Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees)
//         {
//             if (entityTrees.TryGetValue(childAlias, out var direct)) return direct;
//
//             var fallback = entityTrees.Values.FirstOrDefault(t =>
//                 t.Alias.Equals(childAlias, StringComparison.OrdinalIgnoreCase));
//
//             if (fallback is null)
//             {
//                 Console.ForegroundColor = ConsoleColor.Yellow;
//                 Console.WriteLine($"[WARN] Child entity tree '{childAlias}' not found. Skipping.");
//                 Console.ResetColor();
//             }
//
//             return fallback;
//         }
//
//         private static ModelNodeTree? ResolveModelTreeForEntity(
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree entityTree, string childAlias, Dictionary<string, ModelNodeTree> modelTrees) =>
//             modelTrees.Values.FirstOrDefault(t => t.Alias.Equals(childAlias, StringComparison.OrdinalIgnoreCase))
//             ?? modelTrees.Values.FirstOrDefault(t => t.ModelType == entityTree.ModelType);
//
//         // ModelKey.To is a *type name* by construction (NodeBuilder.InferModelChildren /
//         // manually-declared `new ModelKey { To = nameof(SomeType) }`) - alias is only a fallback
//         // for mappings where alias happens to equal the type name (empty Prefix).
//         private static ModelNodeTree? ResolveModelTreeByNameOrAlias(
//             string name, Dictionary<string, ModelNodeTree> modelTrees) =>
//             modelTrees.Values.FirstOrDefault(t => t.ModelType.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
//             ?? modelTrees.Values.FirstOrDefault(t => t.Alias.Equals(name, StringComparison.OrdinalIgnoreCase));
//
//         private static ModelNodeTree GetRootFromWrapper<TWrapper>(Dictionary<string, ModelNodeTree> modelTrees)
//         {
//             var wrapperType  = typeof(TWrapper);
//             var rootListProp = wrapperType
//                 .GetProperties(BindingFlags.Public | BindingFlags.Instance)
//                 .FirstOrDefault(p =>
//                     p.PropertyType.IsGenericType &&
//                     p.PropertyType.GetGenericTypeDefinition() == typeof(List<>));
//
//             if (rootListProp is null)
//                 throw new InvalidOperationException($"{wrapperType.Name} has no List<T> root property");
//
//             var rootModelType = rootListProp.PropertyType.GetGenericArguments()[0];
//
//             return modelTrees.Values.FirstOrDefault(t => t.ModelType == rootModelType)
//                 ?? throw new InvalidOperationException(
//                     $"No ModelNodeTree found for root model type '{rootModelType.Name}'");
//         }
//
//         private static CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree? ResolveEntityTree(
//             string? alias, Type entityType, Type? modelType, int seen,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees)
//         {
//             if (!string.IsNullOrEmpty(alias) && entityTrees.TryGetValue(alias, out var byAlias))
//                 return byAlias;
//
//             if (!string.IsNullOrEmpty(alias))
//             {
//                 var caseMatch = entityTrees.Values.FirstOrDefault(t =>
//                     t.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
//                 if (caseMatch is not null) return caseMatch;
//             }
//
//             var candidates = entityTrees.Values
//                 .Where(t => t.EntityType == entityType && (modelType is null || t.ModelType == modelType))
//                 .OrderBy(t => t.Id)
//                 .ToList();
//
//             if (candidates.Count == 0)
//                 candidates = entityTrees.Values
//                     .Where(t => t.EntityType?.Name.Equals(entityType.Name, StringComparison.OrdinalIgnoreCase) == true)
//                     .OrderBy(t => t.Id)
//                     .ToList();
//
//             return seen < candidates.Count ? candidates[seen] : candidates.FirstOrDefault();
//         }
//     }
//
//     public class NodeResult
//     {
//         public object RootObject    { get; set; } = null!;
//         public object CurrentObject { get; set; } = null!;
//
//         public Dictionary<string, List<object>> RawObjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);
//         public Dictionary<string, object> Objects { get; set; } = new(StringComparer.OrdinalIgnoreCase);
//         public Dictionary<string, List<object>> AllObjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);
//
//         public List<Type> ModelTypes { get; set; } = new();
//         public List<CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> ModelCoffeeBeanery.GraphQL.Core.Sql.EntityNodeTrees { get; set; } = new();
//     }
// }