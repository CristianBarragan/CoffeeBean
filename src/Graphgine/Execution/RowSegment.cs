using System.Collections.Generic;
using System.Collections.Immutable;

namespace Graphgine.Execution;

public readonly struct RowSegment
{
    public readonly ushort StorageEntityId;
    public readonly string EntityOutputAlias;
    public readonly int OrdinalStart;
    public readonly int ColumnCount;
    public string? OutputAlias { get; init; }

    public RowSegment(ushort storageEntityId, string entityOutputAlias, int ordinalStart, int columnCount)
    {
        StorageEntityId   = storageEntityId;
        EntityOutputAlias = entityOutputAlias;
        OrdinalStart      = ordinalStart;
        ColumnCount       = columnCount;
    }
}

public sealed class RowLayout
{
    public readonly ImmutableArray<RowSegment> Segments;
    private readonly Dictionary<string, int> _aliasIndex;

    private RowLayout(ImmutableArray<RowSegment> segments, Dictionary<string, int> aliasIndex)
    {
        Segments    = segments;
        _aliasIndex = aliasIndex;
    }

    public static RowLayout FromQueryPlan(in QueryPlan plan)
    {
        var segments = ImmutableArray.CreateBuilder<RowSegment>();
        string? currentAlias = null;
        ushort currentStorageEntityId = 0;
        var runStart = 0;

        for (int i = 0; i <= plan.Columns.Length; i++)
        {
            var isEnd = i == plan.Columns.Length;
            var alias = isEnd ? null : plan.Columns[i].EntityOutputAlias;

            if (!string.Equals(alias, currentAlias, System.StringComparison.Ordinal))
            {
                if (currentAlias is not null)
                {
                    segments.Add(new RowSegment(
                        currentStorageEntityId, currentAlias, runStart, i - runStart));
                }

                if (!isEnd)
                {
                    currentAlias           = alias;
                    currentStorageEntityId = plan.Columns[i].StorageEntityId;
                    runStart               = i;
                }
            }
        }

        var built = segments.ToImmutable();
        var aliasIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < built.Length; i++)
            aliasIndex[built[i].EntityOutputAlias] = i;

        return new RowLayout(built, aliasIndex);
    }

    /// <summary>Index into the materialized row array for the given output alias, or -1 if absent.</summary>
    public int IndexOf(string alias) =>
        _aliasIndex.TryGetValue(alias, out var idx) ? idx : -1;
}