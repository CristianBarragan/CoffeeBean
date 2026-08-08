namespace CoffeeBeanery.GraphQL.Core.Foundation.Abstractions;

/// <summary>Materializes execution data into application objects.</summary>
public interface IMaterializer<in TSource, out TResult>
{
    TResult Materialize(TSource source);
}
