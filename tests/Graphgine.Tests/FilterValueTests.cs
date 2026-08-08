using Graphgine.Execution.Filtering;
using Xunit;

namespace Graphgine.Tests;

public class FilterValueTests
{
    [Fact]
    public void From_WrapsTheGivenValue()
    {
        var value = FilterValue.From("Bob");

        Assert.Equal("Bob", value.Value);
    }

    [Fact]
    public void From_AllowsNull()
    {
        var value = FilterValue.From(null);

        Assert.Null(value.Value);
    }

    [Fact]
    public void NormalizeList_Null_ReturnsEmptyList()
    {
        var result = FilterValue.NormalizeList(null);

        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeList_Enumerable_ReturnsItsItems()
    {
        var result = FilterValue.NormalizeList(new List<object?> { 1, 2, 3 });

        Assert.Equal(new object?[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void NormalizeList_ScalarValue_IsWrappedInSingleItemList()
    {
        var result = FilterValue.NormalizeList("Bob");

        Assert.Equal(new object?[] { "Bob" }, result);
    }
}
