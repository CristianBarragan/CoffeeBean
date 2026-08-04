namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record MutationColumn(
    ColumnReference Column,
    ushort SourceFieldId,
    MutationValueKind ValueKind
);
