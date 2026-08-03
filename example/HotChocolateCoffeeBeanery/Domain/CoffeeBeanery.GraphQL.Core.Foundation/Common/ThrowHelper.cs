namespace CoffeeBeanery.GraphQL.Core.Foundation.Common;
public static class ThrowHelper { public static Exception Invalid(string message)=>new InvalidOperationException(message); }
