#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class MutationFieldMetadata
{
    public ushort FieldId { get; }
    public ushort EntityId { get; }
    public ushort StorageEntityId { get; }
    public ushort ColumnId { get; }
    public bool IsPrimaryKey { get; }
    public bool IsNavigationKey { get; }

    public MutationFieldMetadata(
        ushort fieldId,
        ushort entityId,
        ushort storageEntityId,
        ushort columnId,
        bool isPrimaryKey,
        bool isNavigationKey = false)
    {
        FieldId = fieldId;
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        ColumnId = columnId;
        IsPrimaryKey = isPrimaryKey;
        IsNavigationKey = isNavigationKey;
    }
}

public sealed class MutationEntityMetadata
{
    private readonly Dictionary<ushort, ImmutableArray<MutationFieldMetadata>> _fields;

    public ushort EntityId { get; }
    public ushort StorageEntityId { get; }
    public string Schema { get; }
    public string Table { get; }
    public bool IsRoot { get; }
    public MutationKind Kind { get; }

    public string? GraphName { get; }
    public string? GraphEdgeLabel { get; }
    public string? GraphFromVertex { get; }
    public string? GraphToVertex { get; }
    public string? GraphFromColumn { get; }
    public string? GraphToColumn { get; }

    /// <summary>
    /// FieldId (not ColumnId) of this graph-edge model's from/to key
    /// fields, resolved at codegen time from Graph.From/To.KeyColumn.
    /// Lets MutationRuntimePlanner.EmitGraphMerge identify the from/to
    /// values generically by FieldId equality — no per-model hardcoded
    /// ColumnId references needed. Null for non-graph (MutationKind.Entity)
    /// models.
    /// </summary>
    public ushort? GraphFromFieldId { get; }

    /// <summary>See GraphFromFieldId.</summary>
    public ushort? GraphToFieldId { get; }

    public ImmutableArray<string> PrimaryColumns { get; }

    public MutationEntityMetadata(
        ushort entityId,
        ushort storageEntityId,
        string schema,
        string table,
        bool isRoot,
        MutationKind kind,
        ImmutableArray<string> primaryColumns,
        Dictionary<ushort, ImmutableArray<MutationFieldMetadata>> fields,
        string? graphName = null,
        string? graphEdgeLabel = null,
        string? graphFromVertex = null,
        string? graphToVertex = null,
        string? graphFromColumn = null,
        string? graphToColumn = null,
        ushort? graphFromFieldId = null,
        ushort? graphToFieldId = null)
    {
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        Schema = schema;
        Table = table;
        IsRoot = isRoot;
        Kind = kind;
        PrimaryColumns = primaryColumns;
        _fields = fields;

        GraphName = graphName;
        GraphEdgeLabel = graphEdgeLabel;
        GraphFromVertex = graphFromVertex;
        GraphToVertex = graphToVertex;
        GraphFromColumn = graphFromColumn;
        GraphToColumn = graphToColumn;
        GraphFromFieldId = graphFromFieldId;
        GraphToFieldId = graphToFieldId;
    }

    /// <summary>
    /// Resolves all destination targets for a FieldId. Most fields have
    /// exactly one target; composite models (e.g. Product.Amount ->
    /// Contract.Amount + Transaction.Amount) can have more than one.
    /// </summary>
    public bool TryResolveFields(
        ushort fieldId,
        out ImmutableArray<MutationFieldMetadata> targets)
    {
        return _fields.TryGetValue(fieldId, out targets);
    }

    /// <summary>
    /// Convenience for callers that only care about a single target
    /// (e.g. graph-edge key fields, which never fan out). Returns the
    /// first resolved target.
    /// </summary>
    public bool TryResolveField(
        ushort fieldId,
        out MutationFieldMetadata field)
    {
        if (_fields.TryGetValue(fieldId, out var targets) && targets.Length > 0)
        {
            field = targets[0];
            return true;
        }

        field = null!;
        return false;
    }
}

public enum MutationKind : byte
{
    Entity = 0,
    GraphEdge = 1
}