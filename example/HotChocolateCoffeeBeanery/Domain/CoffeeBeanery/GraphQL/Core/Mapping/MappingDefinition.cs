namespace CoffeeBeanery.GraphQL.Core.Mapping;

public sealed record MappingDefinition
{
    public required Type Model { get; init; }

    public string? Schema { get; init; }

    public bool IsGraph { get; init; }

    public IReadOnlyList<EntityDefinition> Entities { get; init; } = [];

    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];

    public IReadOnlyList<UpsertKeyDefinition> UpsertKeys { get; init; } = [];

    public GraphDefinition? Graph { get; init; }
}

public sealed record UpsertKeyDefinition
{
    public required Type Entity { get; init; }

    public required string Column { get; init; }
}


public sealed record EntityDefinition
{
    public required Type Entity { get; init; }

    public required string ModelKey { get; init; }

    public required string EntityKey { get; init; }

    public bool IsPrimary { get; init; }

    public string? AliasProperty { get; init; }
}


public sealed record FieldDefinition
{
    public required string Source { get; init; }

    public required Type Entity { get; init; }

    public required string Destination { get; init; }

    public EnumMappingDefinition? EnumMapping { get; init; }
}


// Non-generic base so FieldDefinition can reference it
public abstract record EnumMappingDefinition
{
    public abstract Type ModelEnum { get; }

    public abstract Type EntityEnum { get; }
}


// Strongly typed enum mapping
public sealed record EnumMappingDefinition<TModelEnum, TEntityEnum>
    : EnumMappingDefinition
    where TModelEnum : struct, Enum
    where TEntityEnum : struct, Enum
{
    public override Type ModelEnum => typeof(TModelEnum);

    public override Type EntityEnum => typeof(TEntityEnum);


    /// <summary>
    /// Members where source and destination names differ.
    /// All other members are matched automatically by name.
    /// </summary>
    public Dictionary<string, string> Overrides { get; init; } = [];


    /// <summary>
    /// Members intentionally excluded from mapping.
    /// </summary>
    public HashSet<string> Ignore { get; init; } = [];
}


public sealed record GraphDefinition
{
    public required string GraphName { get; init; }

    public required string EdgeLabel { get; init; }

    public required string EdgeKey { get; init; }

    public required VertexDefinition From { get; init; }

    public required VertexDefinition To { get; init; }

    public required string FromJoinColumn { get; init; }

    public required string ToJoinColumn { get; init; }
}


public sealed record VertexDefinition
{
    public required string Label { get; init; }

    public required string KeyColumn { get; init; }

    public string? Alias { get; init; }
}