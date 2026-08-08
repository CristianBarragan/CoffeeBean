using System;
using System.Collections.Generic;

namespace Graphgine.Execution.Filtering;

public sealed class FilterCompilationContext
{
    public ushort EntityId { get; }

    public Dictionary<string, object?> Parameters { get; }
        = new(StringComparer.Ordinal);


    public FilterCompilationContext(
        ushort entityId)
    {
        EntityId = entityId;
    }


    private int _parameterIndex;


    public string AddParameter(
        object? value)
    {
        var name = $"p{_parameterIndex++}";

        Parameters[name] = value;

        return name;
    }
}