namespace Foundgine.Providers.Aot.Generator;

/// <summary>
///     Canonical semantic identity and stable hashing rules shared by runtime
///     identifiers and the AOT generator. The canonical key is intentionally
///     independent of CLR metadata tokens, declaration order, and runtime state.
/// </summary>
internal static class GeneratorSemanticIdentity
{
    public const string EntityNamespace = "entity";
    public const string FieldNamespace = "field";
    public const string RelationshipNamespace = "relationship";
    public const string TableNamespace = "table";
    public const string ColumnNamespace = "column";
    public const string ModelNamespace = "model";
    public const string ConnectionNamespace = "connection";
    public const string AuthorizationNamespace = "authorization";

    public static string EntityKey(string semanticName)
    {
        return Key(EntityNamespace, semanticName);
    }

    public static string FieldKey(string semanticEntityName, string semanticFieldName)
    {
        return Key(FieldNamespace, Pair(semanticEntityName, semanticFieldName));
    }

    public static string RelationshipKey(string semanticEntityName, string semanticRelationshipName)
    {
        return Key(RelationshipNamespace, Pair(semanticEntityName, semanticRelationshipName));
    }

    public static string TableKey(string storageName)
    {
        return Key(TableNamespace, storageName);
    }

    public static string ColumnKey(string storageName, string columnName)
    {
        return Key(ColumnNamespace, Pair(storageName, columnName));
    }

    public static string ModelKey(string semanticName)
    {
        return Key(ModelNamespace, semanticName);
    }

    public static string ConnectionKey(string semanticModelName, string semanticConnectionName)
    {
        return Key(ConnectionNamespace, Pair(semanticModelName, semanticConnectionName));
    }

    public static string AuthorizationKey(string declaringType, string authorizationName)
    {
        return Key(AuthorizationNamespace, Pair(declaringType, authorizationName));
    }

    /// <summary>Computes the Foundgine stable 64-bit identity hash.</summary>
    public static ulong Hash(string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalKey))
            throw new ArgumentException("Canonical identity key is required.", nameof(canonicalKey));

        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;

        foreach (var b in Encoding.UTF8.GetBytes(canonicalKey))
        {
            hash ^= b;
            hash *= prime;
        }

        // Zero is reserved as an invalid/unassigned identity.
        return hash == 0 ? 1UL : hash;
    }

    public static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identity component is required.", parameterName);

        return value.Trim();
    }

    public static ulong ValidateExplicitId(ulong value, string description)
    {
        return value == 0
            ? throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Explicit {description} identity 0 is reserved and cannot be assigned.")
            : value;
    }

    private static string Key(string @namespace, string value)
    {
        return @namespace + ":" + Normalize(value, nameof(value));
    }

    private static string Pair(string left, string right)
    {
        return Normalize(left, nameof(left)) + "." + Normalize(right, nameof(right));
    }
}