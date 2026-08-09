using Graphgine.Sql;
using Xunit;

namespace Graphgine.Tests;

public class SqlValueTypeTests
{
    [Fact]
    public void UpsertKey_CtorAssignsEntityAndKey()
    {
        var key = new UpsertKey("Customer", "id");

        Assert.Equal("Customer", key.Entity);
        Assert.Equal("id", key.Key);
    }

    [Fact]
    public void EntityKey_DefaultsToEmptyStringsAndPath()
    {
        var key = new EntityKey();

        Assert.Equal("", key.From);
        Assert.Equal("", key.AliasFrom);
        Assert.Equal("", key.To);
        Assert.Empty(key.Path);
    }

    [Fact]
    public void EntityKey_Path_AccumulatesJoinHops()
    {
        var key = new EntityKey();

        key.Path.Add(new JoinHop { TableName = "customer", FromColumn = "id", ToColumn = "customer_id" });
        key.Path.Add(new JoinHop { TableName = "account", FromColumn = "id", ToColumn = "account_id" });

        Assert.Equal(2, key.Path.Count);
        Assert.Equal("customer", key.Path[0].TableName);
        Assert.Equal("account", key.Path[1].TableName);
    }

    [Fact]
    public void LinkKey_DefaultsToEmptyStrings()
    {
        var link = new LinkKey();

        Assert.Equal("", link.From);
        Assert.Equal("", link.To);
    }

    [Fact]
    public void Pagination_DefaultsToNoCursorsAndZeroPageSize()
    {
        var pagination = new Pagination();

        Assert.Null(pagination.After);
        Assert.Null(pagination.Before);
        Assert.Null(pagination.First);
        Assert.Null(pagination.Last);
        Assert.Equal(0, pagination.PageSize);
        Assert.False(pagination.HasNextPage);
        Assert.False(pagination.HasPreviousPage);
        Assert.Equal(0, pagination.TotalRecordCount.RecordCount);
        Assert.Equal(0, pagination.TotalPageRecords.PageRecords);
    }

    [Fact]
    public void Pagination_ForwardPagingFields_RoundTrip()
    {
        var pagination = new Pagination
        {
            First = 20,
            After = "cursor-1",
            HasNextPage = true,
            StartCursor = "cursor-1",
            EndCursor = "cursor-20",
        };

        Assert.Equal(20, pagination.First);
        Assert.Equal("cursor-1", pagination.After);
        Assert.True(pagination.HasNextPage);
        Assert.Equal("cursor-1", pagination.StartCursor);
        Assert.Equal("cursor-20", pagination.EndCursor);
    }
}

public class QueryResultTests
{
    private sealed class Customer
    {
    }

    [Fact]
    public void QueryResult_DefaultsToEmptyModelsAndNoPaging()
    {
        var result = new QueryResult<Customer>();

        Assert.Empty(result.Models);
        Assert.Empty(result.Cursors);
        Assert.Null(result.StartCursor);
        Assert.Null(result.EndCursor);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void QueryResult_ModelsAndCursors_CanBePopulated()
    {
        var result = new QueryResult<Customer>
        {
            Models = { new Customer(), new Customer() },
            Cursors = { "a", "b" },
            TotalCount = 2,
        };

        Assert.Equal(2, result.Models.Count);
        Assert.Equal(new[] { "a", "b" }, result.Cursors);
        Assert.Equal(2, result.TotalCount);
    }
}
