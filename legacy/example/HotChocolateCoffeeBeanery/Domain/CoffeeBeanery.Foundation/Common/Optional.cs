namespace CoffeeBeanery.GraphQL.Core.Foundation.Common;
public readonly record struct Optional<T>(bool HasValue,T? Value);
