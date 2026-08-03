namespace CoffeeBeanery.GraphQL.Core.Foundation.Common;
public sealed class ValueList<T> : List<T> { public ValueList(){} public ValueList(IEnumerable<T> values):base(values){} }
