namespace Foundgine.Aot;

/// <summary>Marks a storage/entity type for Foundgine compile-time metadata generation.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FoundgineEntityAttribute : Attribute
{
    public FoundgineEntityAttribute(string? name = null) => Name = name;
    public string? Name { get; }
    public string? StorageName { get; init; }
    public ushort Id { get; init; }
}

/// <summary>Marks an application model for compile-time semantic metadata generation.
/// Models are never instantiated, populated, or used as ORM entities by Foundgine.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FoundgineModelAttribute : Attribute
{
    public FoundgineModelAttribute(string? name = null) => Name = name;
    public string? Name { get; }
    public ushort Id { get; init; }
}

/// <summary>Overrides generated field metadata for a scalar entity property.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FoundgineFieldAttribute : Attribute
{
    public FoundgineFieldAttribute(string? name = null) => Name = name;
    public string? Name { get; }
    public string? StorageName { get; init; }
    public ushort Id { get; init; }
    public bool IsPrimaryKey { get; init; }
}

/// <summary>Declares a storage relationship. EF remains the authoritative
/// source for relational schema and foreign-key configuration.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FoundgineRelationshipAttribute : Attribute
{
    public FoundgineRelationshipAttribute(Type target, string foreignKey, string principalKey)
    {
        Target = target;
        ForeignKey = foreignKey;
        PrincipalKey = principalKey;
    }

    public Type Target { get; }
    public string ForeignKey { get; }
    public string PrincipalKey { get; }
    public ushort Id { get; init; }
    public string? Name { get; init; }
}

/// <summary>Declares a semantic model connection. It describes a visit from a
/// model to a known entity; it does not describe object construction or runtime
/// object mapping. A connection may optionally be represented by a plain LINQ
/// expression projection whose values are analyzed at compile time.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FoundgineConnectionAttribute : Attribute
{
    public FoundgineConnectionAttribute(Type target) => Target = target;
    public Type Target { get; }
    public ushort Id { get; init; }
    public string? Name { get; init; }
}
