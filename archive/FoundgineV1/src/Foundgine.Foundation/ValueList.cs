namespace Foundgine.Foundation;
public sealed class ValueList<T> : List<T> { public ValueList(){} public ValueList(IEnumerable<T> values):base(values){} }
