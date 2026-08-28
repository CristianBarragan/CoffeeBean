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
    /// <summary>Creates a semantic connection without coupling the model to a storage/entity type.
    /// The target is supplied by <see cref="FoundgineConnectionMapAttribute"/> in the schema/infrastructure layer.</summary>
    public FoundgineConnectionAttribute()
    {
    }

    /// <summary>Legacy overload retained for compatibility. New code should use the parameterless
    /// form and an explicit <see cref="FoundgineConnectionMapAttribute"/>.</summary>
    public FoundgineConnectionAttribute(Type target) => Target = target;

    public Type? Target { get; }
    public ushort Id { get; init; }
    public string? Name { get; init; }
}

/// <summary>Explicitly maps a semantic model connection to a storage/entity type.
/// This declaration belongs in the schema/infrastructure boundary so the model and
/// storage entity do not need to reference one another.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class FoundgineConnectionMapAttribute : Attribute
{
    public FoundgineConnectionMapAttribute(Type model, string connectionMember, Type entity)
    {
        Model = model;
        ConnectionMember = connectionMember;
        Entity = entity;
    }

    public Type Model { get; }
    public string ConnectionMember { get; }
    public Type Entity { get; }
}

/// <summary>Explicitly maps a semantic model to its persistence/entity representation.
/// The mapping is kept outside both types so neither side depends on the other.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class FoundgineModelEntityMapAttribute : Attribute
{
    public FoundgineModelEntityMapAttribute(Type model, Type entity)
    {
        Model = model;
        Entity = entity;
    }

    public Type Model { get; }
    public Type Entity { get; }
}


/// <summary>Declares an AOT authorization predicate for a semantic connection.
/// The property should expose a plain LINQ expression such as
/// <c>Expression&lt;Func&lt;UserContext, Account, bool&gt;&gt;</c>. Foundgine analyzes
/// the expression at build time; it does not invoke it to populate objects.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FoundgineAuthorizationAttribute : Attribute
{
    public FoundgineAuthorizationAttribute(ushort connectionId) => ConnectionId = connectionId;
    public ushort ConnectionId { get; }
    public ushort Id { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Declares a named, consumer-neutral semantic schema. The schema is a boundary
/// for semantic composition; it is not an Agent, MCP, GraphQL, or transport type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FoundgineSchemaAttribute : Attribute
{
    public FoundgineSchemaAttribute(string name) => Name = name;
    public string Name { get; }
}

/// <summary>
/// Declares a consumer-neutral semantic capability from a mapping type. The
/// target type and implementation method are referenced without decorating the
/// domain type with Agent, MCP, GraphQL, or other consumer concepts.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class FoundgineCapabilityAttribute : Attribute
{
    public FoundgineCapabilityAttribute(Type targetType, string schema, string name, string methodName)
    {
        TargetType = targetType;
        Schema = schema;
        Name = name;
        MethodName = methodName;
    }

    public Type TargetType { get; }
    public string Schema { get; }
    public string Name { get; }
    public string MethodName { get; }
    public string? Operation { get; init; }
    public string? Description { get; init; }
}
