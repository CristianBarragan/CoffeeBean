using Xunit;

namespace Foundgine.Foundation.Tests;

public class GuardTests
{
    [Fact]
    public void NotNull_ReturnsValue_WhenNotNull()
    {
        var value = "hello";

        var result = Guard.NotNull(value, nameof(value));

        Assert.Same(value, result);
    }

    [Fact]
    public void NotNull_Throws_WhenNull()
    {
        string? value = null;

        var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(value, "value"));

        Assert.Equal("value", ex.ParamName);
    }
}

public class OptionalTests
{
    [Fact]
    public void DefaultOptional_HasNoValue()
    {
        var optional = default(Optional<int>);

        Assert.False(optional.HasValue);
        Assert.Equal(0, optional.Value);
    }

    [Fact]
    public void Optional_WithValue_ExposesIt()
    {
        var optional = new Optional<string>(true, "abc");

        Assert.True(optional.HasValue);
        Assert.Equal("abc", optional.Value);
    }

    [Fact]
    public void Optional_Equality_IsStructural()
    {
        var a = new Optional<int>(true, 5);
        var b = new Optional<int>(true, 5);
        var c = new Optional<int>(true, 6);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}

public class ResultTests
{
    [Fact]
    public void Ok_IsSuccess_AndCarriesValue()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_IsNotSuccess_AndCarriesError()
    {
        var result = Result<int>.Fail("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.Error);
        Assert.Equal(default, result.Value);
    }
}

public class ValueListTests
{
    [Fact]
    public void ParameterlessCtor_StartsEmpty()
    {
        var list = new ValueList<int>();

        Assert.Empty(list);
    }

    [Fact]
    public void EnumerableCtor_CopiesItems()
    {
        var list = new ValueList<int>(new[] { 1, 2, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void BehavesAsAList()
    {
        var list = new ValueList<string> { "a" };
        list.Add("b");

        Assert.Equal(2, list.Count);
        Assert.Equal("b", list[1]);
    }
}

public class ThrowHelperTests
{
    [Fact]
    public void Invalid_BuildsInvalidOperationException_WithMessage()
    {
        var ex = ThrowHelper.Invalid("nope");

        var ioe = Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("nope", ioe.Message);
    }
}
