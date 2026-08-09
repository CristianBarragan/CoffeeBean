using Graphgine.Mapping;
using Xunit;

namespace Graphgine.Tests;

public class ExpressionHelperTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void GetMemberName_DirectPropertyAccess_ReturnsPropertyName()
    {
        var name = ExpressionHelper.GetMemberName<Customer, int>(c => c.Id);

        Assert.Equal("Id", name);
    }

    [Fact]
    public void GetMemberName_UnwrapsBoxingConversion()
    {
        // m => (object)m.Id -- common when TProperty is object.
        var name = ExpressionHelper.GetMemberName<Customer, object>(c => c.Id);

        Assert.Equal("Id", name);
    }

    [Fact]
    public void GetMemberName_StringProperty_ReturnsPropertyName()
    {
        var name = ExpressionHelper.GetMemberName<Customer, string?>(c => c.Name);

        Assert.Equal("Name", name);
    }

    [Fact]
    public void GetMemberName_NonMemberExpression_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ExpressionHelper.GetMemberName<Customer, int>(c => c.Id + 1));

        Assert.Contains("not a simple property access", ex.Message);
    }
}
