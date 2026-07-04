using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct UpsertRow
{
    public readonly ushort EntityId;
    public readonly string EntityOutputAlias;
    public readonly ImmutableArray<FieldValue> Values;

    /// <summary>
    /// Explicit schema override. When set, PostgresSqlWriter uses this instead
    /// of EntityMeta.Schema[EntityId]. Used by composite model planners where
    /// the EntityId is the model (e.g. CustomerCustomerEdge) but the target
    /// table belongs to a different entity (e.g. CustomerCustomerRelationship).
    /// </summary>
    public readonly string? SchemaOverride;

    /// <summary>
    /// Explicit table override. When set, PostgresSqlWriter uses this instead
    /// of EntityMeta.Table[EntityId].
    /// </summary>
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

public readonly struct MutationPlan
{
    public readonly ImmutableArray<UpsertRow> Rows;

    public MutationPlan(ImmutableArray<UpsertRow> rows) => Rows = rows;
}

public ref struct MutationPlanBuilder
{
    private InlineArray32<UpsertRow> _rows;
    private int _rowCount;

    public void AddRow(
        ushort entityId,
        string outputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null)
    {
        _rows[_rowCount++] = new UpsertRow(entityId, outputAlias, values, schemaOverride, tableOverride);
    }

    public MutationPlan Build()
    {
        var rows = ImmutableArray.CreateBuilder<UpsertRow>(_rowCount);

        for (var i = 0; i < _rowCount; i++)
            rows.Add(_rows[i]);

        return new MutationPlan(rows.ToImmutable());
    }
}