using Graphgine.Execution.Filtering;
using HotChocolate.Language;
using Xunit;

namespace Graphgine.HotChocolate.Tests;

public class WhereCompilerTests
{
    private static ObjectValueNode Obj(params ObjectFieldNode[] fields) => new(fields);
    private static ObjectFieldNode Field(string name, IValueNode value) => new(name, value);
    private static StringValueNode Str(string value) => new(value);

    [Fact]
    public void Compile_EmptyObject_ReturnsNull()
    {
        var result = WhereCompiler.Compile(Obj());

        Assert.Null(result);
    }

    [Fact]
    public void Compile_ScalarShorthand_CompilesToEqBinaryExpression()
    {
        // { firstName: "Bob" }
        var where = Obj(Field("firstName", Str("Bob")));

        var result = WhereCompiler.Compile(where);

        var binary = Assert.IsType<BinaryFilterExpression>(result);
        Assert.Equal("firstName", binary.FieldName);
        Assert.Equal(FilterOperator.Eq, binary.Operator);
        Assert.Equal("Bob", binary.Value);
    }

    [Fact]
    public void Compile_ExplicitEqOperator_CompilesToSameShapeAsShorthand()
    {
        // { firstName: { eq: "Bob" } }
        var where = Obj(Field("firstName", Obj(Field("eq", Str("Bob")))));

        var result = WhereCompiler.Compile(where);

        var binary = Assert.IsType<BinaryFilterExpression>(result);
        Assert.Equal("firstName", binary.FieldName);
        Assert.Equal(FilterOperator.Eq, binary.Operator);
        Assert.Equal("Bob", binary.Value);
    }

    [Fact]
    public void Compile_NeqOperator_ProducesNeqBinaryExpression()
    {
        var where = Obj(Field("status", Obj(Field("neq", Str("Closed")))));

        var result = WhereCompiler.Compile(where);

        var binary = Assert.IsType<BinaryFilterExpression>(result);
        Assert.Equal(FilterOperator.Neq, binary.Operator);
    }

    [Fact]
    public void Compile_InOperator_WithListValue_ExtractsAllItems()
    {
        var where = Obj(Field("status", Obj(Field("in",
            new ListValueNode(Str("Open"), Str("Pending"))))));

        var result = WhereCompiler.Compile(where);

        var binary = Assert.IsType<BinaryFilterExpression>(result);
        Assert.Equal(FilterOperator.In, binary.Operator);
        var values = Assert.IsType<object?[]>(binary.Value);
        Assert.Equal(new object?[] { "Open", "Pending" }, values);
    }

    [Fact]
    public void Compile_MultipleFields_CombinesWithAnd()
    {
        // { firstName: "Bob", status: "Open" }
        var where = Obj(
            Field("firstName", Str("Bob")),
            Field("status", Str("Open")));

        var result = WhereCompiler.Compile(where);

        var and = Assert.IsType<AndFilterExpression>(result);
        Assert.Equal(2, and.Expressions.Count);
    }

    [Fact]
    public void Compile_ExplicitAnd_CombinesNestedObjects()
    {
        // { and: [ { firstName: "Bob" }, { status: "Open" } ] }
        var where = Obj(Field("and", new ListValueNode(
            Obj(Field("firstName", Str("Bob"))),
            Obj(Field("status", Str("Open"))))));

        var result = WhereCompiler.Compile(where);

        var and = Assert.IsType<AndFilterExpression>(result);
        Assert.Equal(2, and.Expressions.Count);
    }

    [Fact]
    public void Compile_ExplicitOr_CombinesNestedObjects()
    {
        var where = Obj(Field("or", new ListValueNode(
            Obj(Field("status", Str("Open"))),
            Obj(Field("status", Str("Pending"))))));

        var result = WhereCompiler.Compile(where);

        Assert.IsType<OrFilterExpression>(result);
    }

    [Fact]
    public void Compile_NavigationObject_ProducesNavigationFilterExpression()
    {
        // { customer: { firstName: { eq: "Bob" } } }
        var where = Obj(Field("customer", Obj(Field("firstName", Obj(Field("eq", Str("Bob")))))));

        var result = WhereCompiler.Compile(where);

        var navigation = Assert.IsType<NavigationFilterExpression>(result);
        Assert.Equal("customer", navigation.NavigationName);
        var inner = Assert.IsType<BinaryFilterExpression>(navigation.Inner);
        Assert.Equal("firstName", inner.FieldName);
    }

    [Theory]
    [InlineData("some", FilterOperator.Some)]
    [InlineData("all", FilterOperator.All)]
    [InlineData("none", FilterOperator.None)]
    public void Compile_CollectionOperators_ProduceCollectionFilterExpression(string keyword, FilterOperator expected)
    {
        var where = Obj(Field(keyword, Obj(Field("status", Str("Open")))));

        var result = WhereCompiler.Compile(where);

        var collection = Assert.IsType<CollectionFilterExpression>(result);
        Assert.Equal(expected, collection.Operator);
    }
}

public class OrderCompilerTests
{
    private static ObjectValueNode Obj(params ObjectFieldNode[] fields) => new(fields);
    private static ObjectFieldNode Field(string name, IValueNode value) => new(name, value);

    [Fact]
    public void Compile_NullOrder_ReturnsEmptyList()
    {
        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(null);

        Assert.Empty(terms);
    }

    [Fact]
    public void Compile_SingleRootField_Ascending()
    {
        // { accountNumber: ASC }
        var order = Obj(Field("accountNumber", new EnumValueNode("ASC")));

        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(order);

        var term = Assert.Single(terms);
        Assert.Equal(new[] { "accountNumber" }, term.Path);
        Assert.Equal(Graphgine.Execution.Ordering.SortDirection.Asc, term.Direction);
    }

    [Fact]
    public void Compile_DescIsCaseInsensitive()
    {
        var order = Obj(Field("balance", new EnumValueNode("desc")));

        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(order);

        Assert.Equal(Graphgine.Execution.Ordering.SortDirection.Desc, terms[0].Direction);
    }

    [Fact]
    public void Compile_UnrecognizedDirection_DefaultsToAscending()
    {
        var order = Obj(Field("balance", new EnumValueNode("SIDEWAYS")));

        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(order);

        Assert.Equal(Graphgine.Execution.Ordering.SortDirection.Asc, terms[0].Direction);
    }

    [Fact]
    public void Compile_NestedNavigation_BuildsFullPath()
    {
        // { customer: { firstName: ASC } }
        var order = Obj(Field("customer", Obj(Field("firstName", new EnumValueNode("ASC")))));

        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(order);

        var term = Assert.Single(terms);
        Assert.Equal(new[] { "customer", "firstName" }, term.Path);
    }

    [Fact]
    public void Compile_MultipleFields_ProducesOneTermPerField_InDeclaredOrder()
    {
        var order = Obj(
            Field("lastName", new EnumValueNode("ASC")),
            Field("firstName", new EnumValueNode("DESC")));

        var terms = Graphgine.Execution.Ordering.OrderCompiler.Compile(order);

        Assert.Equal(2, terms.Count);
        Assert.Equal("lastName", terms[0].Path[0]);
        Assert.Equal("firstName", terms[1].Path[0]);
    }
}
