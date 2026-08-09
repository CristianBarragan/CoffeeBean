using Graphgine.Sql;
using HotChocolate.Types.Pagination;

namespace Graphgine.HotChocolate;

public static class ContextResolverHelper
{
    /// <summary>
    /// Generate connection result based on entity node list and pagination.
    ///
    /// REWRITTEN: the previous version assigned each row a fake sequential
    /// cursor ((++index).ToString()) and recomputed HasNextPage/
    /// HasPreviousPage by int.Parse(pagination.After)/(pagination.Before) --
    /// which never worked correctly (Pagination.First/Last/Before were never
    /// actually populated by any caller) and would now throw outright,
    /// since After/StartCursor/EndCursor are real base64-encoded keyset
    /// cursors, not parseable integers. This version trusts the real,
    /// per-row cursors and real HasNextPage/HasPreviousPage the caller
    /// already computed from the actual DB fetch (see
    /// ProcessService.QueryProcessAsyncViaFoundationPaged) rather than
    /// re-deriving them here.
    /// </summary>
    public static Connection<T> GenerateConnection<T>(
        IReadOnlyList<EntityNode<T>> entityNodes,
        IReadOnlyList<string> cursors,
        Pagination pagination)
        where T : class
    {
        var edges = new List<Edge<T>>(entityNodes.Count);

        for (var i = 0; i < entityNodes.Count; i++)
        {
            var cursor =
                i < cursors.Count
                    ? cursors[i]
                    : string.Empty;

            edges.Add(new Edge<T>(entityNodes[i].Entity, cursor));
        }

        var connectionInfo =
            new ConnectionPageInfo(
                pagination.HasNextPage,
                pagination.HasPreviousPage,
                pagination.StartCursor,
                pagination.EndCursor);

        return new Connection<T>(
            edges,
            connectionInfo,
            pagination.TotalRecordCount?.RecordCount ?? 0);
    }
}

public class CursorResult<T>
{
    public CursorResult(T entity, string cursor, string key)
    {
        Cursor = cursor;
        Entity = entity;
        Key = key;
    }

    public T Entity { get; }
    public string Cursor { get; }
    public string Key { get; set; }
}

public class EntityNode<T> where T : class
{
    public EntityNode(T entity, string key)
    {
        Entity = entity;
        Key = key;
    }

    public T Entity { get; set; }
    public string Key { get; set; }
}
