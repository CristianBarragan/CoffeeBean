using Foundgine.Abstractions;

namespace Foundgine.GraphQL.HotChocolate;

public sealed record GraphQLResultField(
    FieldId? Field,
    RelationshipId? Relationship,
    string GraphQLName,
    string Alias,
    GraphQLResultShape? Children = null);

public sealed record GraphQLResultShape(
    IReadOnlyList<GraphQLResultField> Fields);

public sealed record GraphQLQueryAdaptation(
    Foundgine.Semantics.SemanticRequest Request,
    GraphQLResultShape Result);

