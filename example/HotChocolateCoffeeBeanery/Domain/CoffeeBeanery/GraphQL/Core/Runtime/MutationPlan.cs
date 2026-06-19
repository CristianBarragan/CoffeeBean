using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct UpsertRow
{
    public readonly ushort EntityId;
    public readonly string EntityOutputAlias;
    public readonly ImmutableArray<FieldValue> Values;

    public UpsertRow(ushort entityId, string entityOutputAlias, ImmutableArray<FieldValue> values)
    {
        EntityId = entityId;
        EntityOutputAlias = entityOutputAlias;
        Values = values;
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

    public void AddRow(ushort entityId, string outputAlias, ImmutableArray<FieldValue> values)
    {
        _rows[_rowCount++] = new UpsertRow(entityId, outputAlias, values);
    }

    public MutationPlan Build()
    {
        var rows = ImmutableArray.CreateBuilder<UpsertRow>(_rowCount);

        for (var i = 0; i < _rowCount; i++)
            rows.Add(_rows[i]);

        return new MutationPlan(rows.ToImmutable());
    }
}