namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record GraphMetadata(
    GraphId GraphId,
    string GraphName,
    string EdgeLabel,
    string EdgeKeyColumn,
    EntityMetadata EdgeEntity,
    VertexMetadata From,
    VertexMetadata To
);