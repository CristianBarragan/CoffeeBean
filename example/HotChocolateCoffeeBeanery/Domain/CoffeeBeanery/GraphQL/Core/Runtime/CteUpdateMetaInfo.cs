namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class CteUpdateMetaInfo
{
    public required string NavigationAlias { get; init; }

    public required string ForeignKeyColumn { get; init; }

    public required string OwningPrimaryKeyColumn { get; init; }

    public required string RelatedEntityTypeName { get; init; }

    public required string RelatedSurrogateIdColumn { get; init; }

    public required string RelatedNaturalKeyColumn { get; init; }
}