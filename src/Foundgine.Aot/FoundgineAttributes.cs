namespace Foundgine.Aot;

/// <summary>Marks a domain type for Foundgine compile-time metadata generation.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FoundgineEntityAttribute : Attribute
{
    public FoundgineEntityAttribute(string? name = null) => Name = name;
    public string? Name { get; }
    public string? StorageName { get; init; }
    public ushort Id { get; init; }
}

/// <summary>Overrides generated field metadata for a scalar domain property.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FoundgineFieldAttribute : Attribute
{
    public FoundgineFieldAttribute(string? name = null) => Name = name;
    public string? Name { get; }
    public string? StorageName { get; init; }
    public ushort Id { get; init; }
    public bool IsPrimaryKey { get; init; }
}

/// <summary>Declares a semantic relationship and its relational key mapping.</summary>
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

