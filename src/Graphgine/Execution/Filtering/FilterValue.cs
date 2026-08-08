using System;
using System.Collections.Generic;

namespace Graphgine.Execution.Filtering;

public sealed class FilterValue
{
    public object? Value { get; }

    public FilterValue(object? value)
    {
        Value = value;
    }


    public static FilterValue From(object? value)
    {
        return new FilterValue(value);
    }


    public static IReadOnlyList<object?> NormalizeList(
        object? value)
    {
        if (value is null)
            return Array.Empty<object?>();


        if (value is IEnumerable<object?> list)
            return new List<object?>(list);


        return new[]
        {
            value
        };
    }
}