using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct CteResolutionSpec
{
    public readonly string NavigationAlias;
    public readonly string ForeignKeyColumn;
    public readonly string OwningPkColumn;
    public readonly ushort OwningPkFieldId;
    public readonly string RelatedTableAlias;
    public readonly string RelatedSurrogateIdColumn;
    public readonly string RelatedNaturalKeyColumn;

    public CteResolutionSpec(
        string navigationAlias,
        string foreignKeyColumn,
        string owningPkColumn,
        ushort owningPkFieldId,
        string relatedTableAlias,
        string relatedSurrogateIdColumn,
        string relatedNaturalKeyColumn)
    {
        NavigationAlias = navigationAlias;
        ForeignKeyColumn = foreignKeyColumn;
        OwningPkColumn = owningPkColumn;
        OwningPkFieldId = owningPkFieldId;
        RelatedTableAlias = relatedTableAlias;
        RelatedSurrogateIdColumn = relatedSurrogateIdColumn;
        RelatedNaturalKeyColumn = relatedNaturalKeyColumn;
    }
}

public readonly struct UpsertRow
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly string EntityOutputAlias;

    /// <summary>
    /// Literal values written directly into the INSERT.
    /// </summary>
    public readonly ImmutableArray<FieldValue> Values;

    /// <summary>
    /// Foreign-key values resolved from another table using a natural key.
    /// When non-empty the SQL writer should emit INSERT ... SELECT instead of INSERT ... VALUES.
    /// </summary>
    public readonly ImmutableArray<LookupValue> Lookups;

    public readonly string? SchemaOverride;
    public readonly string? TableOverride;

    public bool HasLookups => !Lookups.IsDefaultOrEmpty;

    public UpsertRow(
        ushort entityId,
        ushort storageEntityId,
        string entityOutputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null,
        ImmutableArray<LookupValue> lookups = default)
    {
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        EntityOutputAlias = entityOutputAlias;

        Values =
            values.IsDefault
                ? ImmutableArray<FieldValue>.Empty
                : values;

        SchemaOverride = schemaOverride;
        TableOverride = tableOverride;

        Lookups =
            lookups.IsDefault
                ? ImmutableArray<LookupValue>.Empty
                : lookups;
    }
}

public readonly struct LookupValue
{
    /// <summary>
    /// Column being populated (e.g. InnerCustomerId).
    /// </summary>
    public readonly ushort TargetColumnId;

    /// <summary>
    /// Entity/table to perform the lookup against.
    /// </summary>
    public readonly ushort LookupStorageEntityId;

    /// <summary>
    /// Natural key column (e.g. CustomerKey).
    /// </summary>
    public readonly ushort LookupColumnId;

    /// <summary>
    /// Surrogate key column to return (e.g. Id).
    /// </summary>
    public readonly ushort ResultColumnId;

    /// <summary>
    /// Value to match against the natural key.
    /// </summary>
    public readonly object? LookupValueLiteral;

    /// <summary>
    /// SQL alias for the lookup table.
    /// </summary>
    public readonly string Alias;

    public LookupValue(
        ushort targetColumnId,
        ushort lookupStorageEntityId,
        ushort lookupColumnId,
        ushort resultColumnId,
        object? lookupValueLiteral,
        string alias)
    {
        TargetColumnId = targetColumnId;
        LookupStorageEntityId = lookupStorageEntityId;
        LookupColumnId = lookupColumnId;
        ResultColumnId = resultColumnId;
        LookupValueLiteral = lookupValueLiteral;
        Alias = alias;
    }
}


public readonly struct MutationCteNode
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly string Alias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly ImmutableArray<MutationCteNode> Children;
    public readonly string? SchemaOverride;
    public readonly string? TableOverride;
    public readonly ImmutableArray<string> ConflictColumns;


    public MutationCteNode(
        ushort entityId,
        ushort storageEntityId,
        string alias,
        ImmutableArray<FieldValue> values,
        ImmutableArray<MutationCteNode> children,
        string? schemaOverride = null,
        string? tableOverride = null,
        ImmutableArray<string> conflictColumns = default)
    {
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        Alias = alias;
        Values = values;
        Children = children;
        SchemaOverride = schemaOverride;
        TableOverride = tableOverride;

        ConflictColumns =
            conflictColumns.IsDefault
                ? ImmutableArray<string>.Empty
                : conflictColumns;
    }
}


public readonly struct FieldMapSpec
{
    public readonly string SourceName;
    public readonly ushort DestinationEntity;
    public readonly string DestinationName;
    public readonly string SourceAlias;
    public readonly string DestinationAlias;


    public FieldMapSpec(
        string sourceName,
        ushort destinationEntity,
        string destinationName,
        string sourceAlias,
        string destinationAlias)
    {
        SourceName = sourceName;
        DestinationEntity = destinationEntity;
        DestinationName = destinationName;
        SourceAlias = sourceAlias;
        DestinationAlias = destinationAlias;
    }
}


public readonly struct MutationPlan
{
    public readonly ImmutableArray<UpsertRow> Rows;
    public readonly ImmutableArray<MutationCteNode> CteRoots;
    public readonly ImmutableArray<GraphMergeSpec> GraphMerges;


    public MutationPlan(
        ImmutableArray<UpsertRow> rows)
    {
        Rows = rows;
        CteRoots = ImmutableArray<MutationCteNode>.Empty;
        GraphMerges = ImmutableArray<GraphMergeSpec>.Empty;
    }


    public MutationPlan(
        ImmutableArray<UpsertRow> rows,
        ImmutableArray<MutationCteNode> cteRoots)
    {
        Rows = rows;
        CteRoots = cteRoots;
        GraphMerges = ImmutableArray<GraphMergeSpec>.Empty;
    }


    public MutationPlan(
        ImmutableArray<UpsertRow> rows,
        ImmutableArray<MutationCteNode> cteRoots,
        ImmutableArray<GraphMergeSpec> graphMerges)
    {
        Rows = rows;
        CteRoots = cteRoots;
        GraphMerges = graphMerges;
    }
    
    public bool HasCte => !CteRoots.IsEmpty;

    public bool HasGraphMerges => !GraphMerges.IsEmpty;
}