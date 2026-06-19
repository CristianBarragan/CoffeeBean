namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

internal static class AliasPathBuilder
{
    public static string Build(string parent, string current)
    {
        if (string.IsNullOrEmpty(parent))
            return current;

        return $"{parent}{current}";
    }
}