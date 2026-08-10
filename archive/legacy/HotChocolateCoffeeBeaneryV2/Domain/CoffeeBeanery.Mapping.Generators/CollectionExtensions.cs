using System;
using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators;

internal static class ReadOnlyListExtensions
{
    internal static int FindIndex<T>(
        this IReadOnlyList<T> items,
        Func<T, bool> predicate)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                return i;
            }
        }

        return -1;
    }
}