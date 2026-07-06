using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct UpsertRow
{
    public readonly ushort EntityId;
    public readonly string EntityOutputAlias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly string? SchemaOverride;
    public readonly string? TableOverride;

    public UpsertRow(
        ushort entityId,
        string entityOutputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null)
    {
        EntityId = entityId;
        EntityOutputAlias = entityOutputAlias;
        Values = values;
        SchemaOverride = schemaOverride;
        TableOverride = tableOverride;
    }
}

public readonly struct CteResolutionSpec
{
    public readonly string NavigationAlias;           // "InnerCustomer"
    public readonly string ForeignKeyColumn;          // "InnerCustomerId"
    public readonly string OwningPkColumn;            // "CustomerCustomerRelationshipKey"
    public readonly ushort OwningPkFieldId;           // index into root.Values to find the PK value
    public readonly string RelatedTableAlias;         // "InnerCustomerCustomer"
    public readonly string RelatedSurrogateIdColumn;  // "Id"
    public readonly string RelatedNaturalKeyColumn;   // "CustomerKey"

    public CteResolutionSpec(
        string navigationAlias,
        string foreignKeyColumn,
        string owningPkColumn,
        ushort owningPkFieldId,
        string relatedTableAlias,
        string relatedSurrogateIdColumn,
        string relatedNaturalKeyColumn)
    {
        NavigationAlias = navigationAlias;
        ForeignKeyColumn = foreignKeyColumn;
        OwningPkColumn = owningPkColumn;
        OwningPkFieldId = owningPkFieldId;
        RelatedTableAlias = relatedTableAlias;
        RelatedSurrogateIdColumn = relatedSurrogateIdColumn;
        RelatedNaturalKeyColumn = relatedNaturalKeyColumn;
    }
}

public readonly struct MutationCteNode
{
    public readonly ushort EntityId;
    public readonly string Alias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly ImmutableArray<MutationCteNode> Children;

    /// <summary>
    /// For composite models whose EntityId maps to the model (e.g. CustomerCustomerEdge)
    /// rather than the real DB table (CustomerCustomerRelationship).
    /// PostgresSqlWriter uses this instead of EntityMeta.Schema[EntityId].
    /// </summary>
    public readonly string? SchemaOverride;

    /// <summary>
    /// For composite models — the real DB table name.
    /// PostgresSqlWriter uses this instead of EntityMeta.Table[EntityId].
    /// </summary>
    public readonly string? TableOverride;

    /// <summary>
    /// Conflict columns for this specific CTE node.
    /// When non-empty, PostgresSqlWriter uses these instead of EntityMeta.ConflictColumns[EntityId].
    /// Baked in at code-generation time from the mapping's UpsertKeys.
    /// </summary>
    public readonly ImmutableArray<string> ConflictColumns;

    public MutationCteNode(
        ushort entityId,
        string alias,
        ImmutableArray<FieldValue> values,
        ImmutableArray<MutationCteNode> children,
        string? schemaOverride = null,
        string? tableOverride = null,
        ImmutableArray<string> conflictColumns = default)
    {
        EntityId = entityId;
        Alias = alias;
        Values = values;
        Children = children;
        SchemaOverride = schemaOverride;
        TableOverride = tableOverride;
        ConflictColumns = conflictColumns.IsDefault
            ? ImmutableArray<string>.Empty
            : conflictColumns;
    }
}

public readonly struct MutationPlan
{
    public readonly ImmutableArray<UpsertRow> Rows;
    public readonly ImmutableArray<MutationCteNode> CteRoots;

    public MutationPlan(ImmutableArray<UpsertRow> rows)
    {
        Rows = rows;
        CteRoots = ImmutableArray<MutationCteNode>.Empty;
    }

    public MutationPlan(
        ImmutableArray<UpsertRow> rows,
        ImmutableArray<MutationCteNode> cteRoots)
    {
        Rows = rows;
        CteRoots = cteRoots;
    }

    public bool HasCte => !CteRoots.IsEmpty;
}

public ref struct MutationPlanBuilder
{
    private InlineArray32<UpsertRow> _rows;
    private int _rowCount;

    private InlineArray32<MutationCteNode> _cteRoots;
    private int _cteRootCount;

    public void AddRow(
        ushort entityId,
        string outputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null)
    {
        _rows[_rowCount++] = new UpsertRow(entityId, outputAlias, values, schemaOverride, tableOverride);
    }

    public void AddCteRoot(MutationCteNode node)
    {
        _cteRoots[_cteRootCount++] = node;
    }

    public MutationPlan Build()
    {
        var rows = ImmutableArray.CreateBuilder<UpsertRow>(_rowCount);
        for (var i = 0; i < _rowCount; i++)
            rows.Add(_rows[i]);

        if (_cteRootCount == 0)
            return new MutationPlan(rows.ToImmutable());

        var roots = ImmutableArray.CreateBuilder<MutationCteNode>(_cteRootCount);
        for (var i = 0; i < _cteRootCount; i++)
            roots.Add(_cteRoots[i]);

        return new MutationPlan(rows.ToImmutable(), roots.ToImmutable());
    }
}