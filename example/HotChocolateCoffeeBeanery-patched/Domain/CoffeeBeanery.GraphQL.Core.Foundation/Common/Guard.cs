namespace CoffeeBeanery.GraphQL.Core.Foundation.Common;
public static class Guard { public static T NotNull<T>(T? value,string name) where T:class => value??throw new ArgumentNullException(name); }
