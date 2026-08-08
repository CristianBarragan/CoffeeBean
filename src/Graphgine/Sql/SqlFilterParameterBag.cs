using System.Collections.Generic;

namespace Graphgine.Sql;

public sealed class SqlFilterParameterBag
{
    private int _index;


    public Dictionary<string, object?> Values { get; }
        = new();



    public string Add(
        object? value)
    {
        var name =
            $"p{_index++}";


        Values[name] =
            value;


        return name;
    }
}