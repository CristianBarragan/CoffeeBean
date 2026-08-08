using System;
using System.Collections.Generic;

namespace Graphgine.SourceGenerators;

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