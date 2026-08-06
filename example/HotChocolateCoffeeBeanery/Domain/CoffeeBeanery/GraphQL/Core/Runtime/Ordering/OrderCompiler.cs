using System;
using System.Collections.Generic;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Ordering;

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// One sort term: Path is the navigation path to the field (e.g.
/// ["customer", "firstNaming"] for `{ customer: { firstNaming: ASC } }`,
/// or just ["accountNumber"] for a root-level field).
/// </summary>
public sealed record OrderTerm(
    IReadOnlyList<string> Path,
    SortDirection Direction);

/// <summary>
/// Parses the `order` argument's raw GraphQL AST value (the shape
/// DynamicSortModule's dynamically-generated {Model}SortInput types
/// produce) into a flat, ordered list of OrderTerm. Mirrors WhereCompiler's
/// role for the `where` argument -- a small, self-contained AST walk, not
/// tied to any specific model's generated input type.
/// </summary>
public static class OrderCompiler
{
    public static List<OrderTerm> Compile(IValueNode? order)
    {
        var terms = new List<OrderTerm>();

        if (order is ObjectValueNode obj)
            Walk(obj, new List<string>(), terms);

        return terms;
    }

    private static void Walk(
        ObjectValueNode node,
        List<string> path,
        List<OrderTerm> terms)
    {
        foreach (var field in node.Fields)
        {
            var childPath =
                new List<string>(path) { field.Name.Value };

            switch (field.Value)
            {
                case EnumValueNode enumNode:

                    var direction =
                        string.Equals(
                            enumNode.Value,
                            "DESC",
                            StringComparison.OrdinalIgnoreCase)
                            ? SortDirection.Desc
                            : SortDirection.Asc;

                    terms.Add(
                        new OrderTerm(childPath, direction));

                    break;

                case ObjectValueNode nested:

                    Walk(nested, childPath, terms);
                    break;
            }
        }
    }
}
