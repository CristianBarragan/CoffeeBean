// #nullable enable
//
// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Linq;
// using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public sealed class MutationEntityMetadata
// {
//     private readonly Dictionary<ushort, ImmutableArray<MutationFieldMetadata>> _fields;
//
//
//     public ushort EntityId { get; }
//
//     public ushort StorageEntityId { get; }
//
//     public string Schema { get; }
//
//     public string Table { get; }
//
//     public ImmutableArray<string> PrimaryColumns { get; }
//
//     public ImmutableArray<MutationStorageEntityMetadata> StorageEntities { get; }
//
//     public ImmutableArray<CteUpdateMetaInfo> CteUpdateMeta { get; }
//
//     public bool IsRoot { get; }
//
//     public MutationKind Kind { get; }
//
//
//     public string? GraphName { get; }
//
//     public string? GraphEdgeLabel { get; }
//
//     public string? GraphFromVertex { get; }
//
//     public string? GraphToVertex { get; }
//
//     public string? GraphFromColumn { get; }
//
//     public string? GraphToColumn { get; }
//
//
//     /// <summary>
//     /// FieldId for graph edge source navigation key.
//     /// </summary>
//     public ushort? GraphFromFieldId { get; }
//
//
//     /// <summary>
//     /// FieldId for graph edge destination navigation key.
//     /// </summary>
//     public ushort? GraphToFieldId { get; }
//
//
//     public MutationEntityMetadata(
//         ushort entityId,
//         ushort storageEntityId,
//         string schema,
//         string table,
//         bool isRoot,
//         MutationKind kind,
//         ImmutableArray<string> primaryColumns,
//         Dictionary<ushort, ImmutableArray<MutationFieldMetadata>> fields,
//         ImmutableArray<CteUpdateMetaInfo> cteUpdateMeta,
//         ImmutableArray<MutationStorageEntityMetadata> storageEntities = default,
//         string? graphName = null,
//         string? graphEdgeLabel = null,
//         string? graphFromVertex = null,
//         string? graphToVertex = null,
//         string? graphFromColumn = null,
//         string? graphToColumn = null,
//         ushort? graphFromFieldId = null,
//         ushort? graphToFieldId = null)
//     {
//         EntityId = entityId;
//         StorageEntityId = storageEntityId;
//
//         Schema = schema;
//         Table = table;
//
//         IsRoot = isRoot;
//         Kind = kind;
//
//         PrimaryColumns = primaryColumns;
//
//         _fields = fields;
//
//         CteUpdateMeta = cteUpdateMeta;
//
//
//         StorageEntities =
//             storageEntities.IsDefaultOrEmpty
//                 ? ImmutableArray.Create(
//                     new MutationStorageEntityMetadata(
//                         storageEntityId,
//                         schema,
//                         table))
//                 : storageEntities;
//
//
//         GraphName = graphName;
//         GraphEdgeLabel = graphEdgeLabel;
//
//         GraphFromVertex = graphFromVertex;
//         GraphToVertex = graphToVertex;
//
//         GraphFromColumn = graphFromColumn;
//         GraphToColumn = graphToColumn;
//
//         GraphFromFieldId = graphFromFieldId;
//         GraphToFieldId = graphToFieldId;
//     }
//
//
//     public bool IsNavigationField(ushort fieldId)
//     {
//         return _fields.Values
//             .SelectMany(x => x)
//             .Any(x =>
//                 x.FieldId == fieldId &&
//                 x.IsNavigationKey);
//     }
//
//
//     public bool TryResolveFields(
//         ushort fieldId,
//         out ImmutableArray<MutationFieldMetadata> targets)
//     {
//         return _fields.TryGetValue(
//             fieldId,
//             out targets);
//     }
//
//
//     public bool TryResolveField(
//         ushort fieldId,
//         out MutationFieldMetadata field)
//     {
//         if (_fields.TryGetValue(fieldId, out var targets) &&
//             targets.Length > 0)
//         {
//             field = targets[0];
//             return true;
//         }
//
//         field = null!;
//         return false;
//     }
// }