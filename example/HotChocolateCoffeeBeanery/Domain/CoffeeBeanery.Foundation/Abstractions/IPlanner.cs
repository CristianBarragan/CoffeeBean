namespace CoffeeBeanery.GraphQL.Core.Foundation.Abstractions;

/// <summary>Creates executable plans from graph requests.</summary>
public interface IPlanner<in TRequest, out TPlan>
{
    TPlan Plan(TRequest request);
}
