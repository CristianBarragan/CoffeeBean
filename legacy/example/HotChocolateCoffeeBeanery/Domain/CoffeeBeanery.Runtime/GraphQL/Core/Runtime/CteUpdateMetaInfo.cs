namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class CteUpdateMetaInfo
{
    public required string NavigationAlias { get; set; }

    public required string ForeignKeyColumn { get; set; }
    public ushort ForeignKeyColumnId { get; set; }


    public required string OwningPrimaryKeyColumn { get; set; }
    public ushort OwningPrimaryKeyColumnId { get; set; }


    public required string RelatedEntityTypeName { get; set; }

    public ushort RelatedStorageEntityId { get; set; }


    public required string RelatedSurrogateIdColumn { get; set; }
    public ushort RelatedSurrogateIdColumnId { get; set; }


    public required string RelatedNaturalKeyColumn { get; set; }
    public ushort RelatedNaturalKeyColumnId { get; set; }
}