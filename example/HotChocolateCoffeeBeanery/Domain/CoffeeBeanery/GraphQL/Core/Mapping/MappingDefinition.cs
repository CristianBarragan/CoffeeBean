namespace CoffeeBeanery.GraphQL.Core.Mapping;

public sealed record MappingDefinition
{
    public required Type Model { get; init; }

    public string? Schema { get; init; }

    public IReadOnlyList<EntityDefinition> Entities { get; init; } = [];

    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];

    public IReadOnlyList<PrimaryKeyDefinition> PrimaryKey { get; init; } = [];

    public IReadOnlyList<UpsertKeyDefinition> UpsertKeys { get; init; } = [];
    
    public MutationDefinition? Mutation { get; init; }

    public GraphDefinition? Graph { get; init; }

    public IReadOnlyList<NavigationDefinition> Navigations { get; init; } = [];
}

public sealed record NavigationDefinition
{
    /// <summary>The property name on the parent Model (e.g. "Product", "ContactPoint").</summary>
    public required string NavigationName { get; init; }

    /// <summary>The target Model type (e.g. typeof(Product)).</summary>
    public required Type TargetModel { get; init; }

    public bool IsCollection { get; init; } = true;

    /// <summary>
    /// One path per backing entity of the target model. For a simple
    /// single-entity target this list has exactly one Path. For a composite
    /// target like Product, one Path per Product-owned entity that is
    /// actually reachable from this parent — omit any that aren't.
    /// </summary>
    public required IReadOnlyList<JoinPathDefinition> Paths { get; init; }
}

public sealed record JoinPathDefinition
{
    /// <summary>Which of the target model's backing entities this path resolves to.</summary>
    public required Type TargetEntity { get; init; }

    /// <summary>Ordered hops from the parent's primary entity to TargetEntity.</summary>
    public required IReadOnlyList<JoinHopDefinition> Hops { get; init; }
}

public sealed record JoinHopDefinition
{
    public required Type FromEntity { get; init; }
    public required string FromColumn { get; init; }
    public required Type ToEntity { get; init; }
    public required string ToColumn { get; init; }
}

public sealed record UpsertKeyDefinition
{
    public required Type Entity { get; init; }

    public required string Column { get; init; }
}

public sealed record PrimaryKeyDefinition
{
    public required Type Entity { get; init; }

    public required string ModelKey { get; init; }
    
    public required string ColumnKey { get; init; }
}

public sealed record MutationDefinition
{
    public IReadOnlyList<MutationEntityDefinition> Entities { get; init; }
        = [];

    public IReadOnlyList<GraphMutationDefinition> Graphs { get; init; }
        = [];
}

public sealed record MutationEntityDefinition
{
    public required Type Entity { get; init; }

    public required string Alias { get; init; }

    public MutationOperation Operation { get; init; } =
        MutationOperation.Upsert;

    public IReadOnlyList<MutationFieldDefinition> Fields { get; init; }
        = [];
}

public sealed record MutationFieldDefinition
{
    public required string ModelField { get; init; }

    public required string Column { get; init; }
}

public sealed record GraphMutationDefinition
{
    public required string EdgeLabel { get; init; }

    public required string FromAlias { get; init; }

    public required string ToAlias { get; init; }

    public string? EdgeKey { get; init; }
}

public enum MutationOperation
{
    Insert,

    Update,

    Upsert,

    Ignore
}


public sealed record EntityDefinition
{
    public Type Entity { get; init; }

    public required string ModelKey { get; init; }

    public string? EntityKey { get; init; }

    public bool IsPrimary { get; init; }

    public string? AliasProperty { get; init; }
}


public sealed record FieldDefinition
{
    public required string Source { get; init; }

    public required Type Entity { get; init; }

    public required string Destination { get; init; }

    public bool IsNavigationKey { get; set; }

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